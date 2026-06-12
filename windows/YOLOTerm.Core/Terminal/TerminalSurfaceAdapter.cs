using System.Text;
using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Pty;

namespace YOLOTerm.Core.Terminal;

/// <summary>
/// Terminal surface implementation wrapping Windows Terminal control (contracts v1)
/// NOTE: Actual Windows Terminal control integration happens in the WPF layer
/// This class provides the headless testing interface
/// </summary>
public class TerminalSurfaceAdapter : ITerminalSurface
{
    private readonly object _lockObject = new();
    private byte[] _buffer = Array.Empty<byte>();
    private int _cols = 80;
    private int _rows = 24;

    // For headless testing: we need access to the underlying terminal state
    // In a real implementation, this would be provided by the Windows Terminal control
    private readonly ITerminalStateProvider? _stateProvider;

    public int Cols
    {
        get { lock (_lockObject) return _cols; }
        private set { lock (_lockObject) _cols = value; }
    }

    public int Rows
    {
        get { lock (_lockObject) return _rows; }
        private set { lock (_lockObject) _rows = value; }
    }

    public TerminalSurfaceAdapter(ITerminalStateProvider? stateProvider = null)
    {
        _stateProvider = stateProvider;
    }

    public void Feed(byte[] data)
    {
        lock (_lockObject)
        {
            var newBuffer = new byte[_buffer.Length + data.Length];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
            Buffer.BlockCopy(data, 0, newBuffer, _buffer.Length, data.Length);
            _buffer = newBuffer;
        }

        _stateProvider?.ProcessData(data);
    }

    public CellMetrics GetCellMetrics()
    {
        return new CellMetrics(9.0, 18.0);
    }

    public Cell? GetCell(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows)
            return null;

        if (_stateProvider == null)
            return new Cell(" ", null, null, CellAttributes.None);

        return _stateProvider.GetCell(col, row);
    }

    public byte[] Serialize()
    {
        lock (_lockObject)
        {
            return (byte[])_buffer.Clone();
        }
    }

    public void Resize(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
    }
}

/// <summary>
/// Interface for accessing terminal state (implemented by Windows Terminal control wrapper)
/// </summary>
public interface ITerminalStateProvider
{
    void ProcessData(byte[] data);
    Cell? GetCell(int col, int row);
}
