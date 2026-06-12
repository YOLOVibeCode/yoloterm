using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace YOLOTerm.Core.Pty;

/// <summary>
/// ConPTY-based PTY session implementation for Windows
/// Implements contracts v1 PTY interfaces
/// </summary>
public sealed class ConPtySession : IPtySession, IDisposable
{
    private readonly SafeFileHandle _inputHandle;
    private readonly SafeFileHandle _outputHandle;
    private readonly SafePseudoConsoleHandle _pseudoConsoleHandle;
    private readonly Process _process;
    private bool _disposed;
    private Action<int>? _onExitHandler;

    public int Pid => _process.Id;
    public bool IsAlive => !_process.HasExited;

    public event Action<int>? OnExit
    {
        add
        {
            _onExitHandler += value;
        }
        remove
        {
            _onExitHandler -= value;
        }
    }

    private ConPtySession(
        SafeFileHandle inputHandle,
        SafeFileHandle outputHandle,
        SafePseudoConsoleHandle pseudoConsoleHandle,
        Process process)
    {
        _inputHandle = inputHandle;
        _outputHandle = outputHandle;
        _pseudoConsoleHandle = pseudoConsoleHandle;
        _process = process;

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            var exitCode = _process.ExitCode;
            _onExitHandler?.Invoke(exitCode);
        };
    }

    public static ConPtySession Spawn(
        string shell,
        string cwd,
        int cols,
        int rows,
        Dictionary<string, string> env)
    {
        if (!File.Exists(shell))
            throw new FileNotFoundException($"Shell not found: {shell}");
        
        if (!Directory.Exists(cwd))
            throw new DirectoryNotFoundException($"Working directory not found: {cwd}");

        var (inputPipeRead, inputPipeWrite) = CreatePipe();
        var (outputPipeRead, outputPipeWrite) = CreatePipe();

        try
        {
            var pseudoConsoleHandle = CreatePseudoConsole(cols, rows, inputPipeRead, outputPipeWrite);
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            foreach (var kvp in env)
            {
                process.StartInfo.Environment[kvp.Key] = kvp.Value;
            }

            AttachPseudoConsole(process, pseudoConsoleHandle);
            
            if (!process.Start())
                throw new InvalidOperationException("Failed to start process");

            inputPipeRead.Close();
            outputPipeWrite.Close();

            return new ConPtySession(inputPipeWrite, outputPipeRead, pseudoConsoleHandle, process);
        }
        catch
        {
            inputPipeRead.Close();
            inputPipeWrite.Close();
            outputPipeRead.Close();
            outputPipeWrite.Close();
            throw;
        }
    }

    public void Write(byte[] data)
    {
        if (_disposed || !IsAlive)
            throw new InvalidOperationException("Session is closed");

        try
        {
            using var stream = new FileStream(_inputHandle, FileAccess.Write, bufferSize: 4096, isAsync: false);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Write failed", ex);
        }
    }

    public void Resize(int cols, int rows)
    {
        if (_disposed || !IsAlive)
            throw new InvalidOperationException("Session is closed");

        var result = NativeMethods.ResizePseudoConsole(_pseudoConsoleHandle, new COORD { X = (short)cols, Y = (short)rows });
        if (result != 0)
            throw new InvalidOperationException($"Resize failed with code {result}");
    }

    public void Kill()
    {
        if (_disposed)
            throw new InvalidOperationException("Session already disposed");
        
        if (!IsAlive)
            throw new InvalidOperationException("Process already exited");

        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Kill failed", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch { }

        _pseudoConsoleHandle.Dispose();
        _inputHandle.Dispose();
        _outputHandle.Dispose();
        _process.Dispose();
    }

    private static (SafeFileHandle read, SafeFileHandle write) CreatePipe()
    {
        if (!NativeMethods.CreatePipe(out var read, out var write, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return (read, write);
    }

    private static SafePseudoConsoleHandle CreatePseudoConsole(int cols, int rows, SafeFileHandle input, SafeFileHandle output)
    {
        var coord = new COORD { X = (short)cols, Y = (short)rows };
        var result = NativeMethods.CreatePseudoConsole(coord, input, output, 0, out var handle);
        if (result != 0)
            throw new Win32Exception(result, "CreatePseudoConsole failed");
        return handle;
    }

    private static void AttachPseudoConsole(Process process, SafePseudoConsoleHandle handle)
    {
        const int STARTF_USESTDHANDLES = 0x00000100;
        const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

        var startupInfo = new STARTUPINFOEX
        {
            StartupInfo =
            {
                cb = Marshal.SizeOf<STARTUPINFOEX>()
            }
        };

        IntPtr lpSize = IntPtr.Zero;
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

        if (!NativeMethods.InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref lpSize))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            if (!NativeMethods.UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                (IntPtr)22,
                handle.DangerousGetHandle(),
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            typeof(ProcessStartInfo)
                .GetProperty("StartupInfoW", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(process.StartInfo, startupInfo);
        }
        finally
        {
            if (startupInfo.lpAttributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            }
        }
    }
}
