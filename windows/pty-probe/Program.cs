// pty-probe.exe - Test binary for PTY lifecycle tests (contracts v1)
// Mirrors the Swift version from Track A (A1.4)
//
// Behavior:
// 1. Prints "PTY_PROBE_READY" marker
// 2. Echoes stdin to stdout
// 3. Exits on "exit\n" or signal

using System.Text;

Console.WriteLine("PTY_PROBE_READY");
Console.Out.Flush();

var buffer = new byte[4096];
var stream = Console.OpenStandardInput();

while (true)
{
    var bytesRead = stream.Read(buffer, 0, buffer.Length);
    if (bytesRead == 0)
        break;

    var input = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    Console.Write(input);
    Console.Out.Flush();

    if (input.Contains("exit\n") || input.Contains("exit\r\n"))
        break;
}

Environment.Exit(0);
