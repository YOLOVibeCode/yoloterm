using System.Text;
using YOLOTerm.Core.Contracts;

namespace YOLOTerm.Core.Terminal;

/// <summary>
/// Basic VT100/ANSI parser for headless testing
/// This provides a minimal implementation for golden tests
/// The real rendering happens in the Windows Terminal control
/// </summary>
public class HeadlessTerminalState : ITerminalStateProvider
{
    private readonly Cell[,] _cells;
    private readonly int _cols;
    private readonly int _rows;
    private int _cursorCol;
    private int _cursorRow;
    private Color? _currentFg;
    private Color? _currentBg;
    private CellAttributes _currentAttrs;

    // ANSI 16-color palette (xterm colors)
    private static readonly Color[] AnsiColors = new[]
    {
        new Color(0x00, 0x00, 0x00), // Black
        new Color(0xcd, 0x00, 0x00), // Red
        new Color(0x00, 0xcd, 0x00), // Green
        new Color(0xcd, 0xcd, 0x00), // Yellow
        new Color(0x00, 0x00, 0xee), // Blue
        new Color(0xcd, 0x00, 0xcd), // Magenta
        new Color(0x00, 0xcd, 0xcd), // Cyan
        new Color(0xe5, 0xe5, 0xe5), // White
        new Color(0x7f, 0x7f, 0x7f), // Bright Black
        new Color(0xff, 0x00, 0x00), // Bright Red
        new Color(0x00, 0xff, 0x00), // Bright Green
        new Color(0xff, 0xff, 0x00), // Bright Yellow
        new Color(0x5c, 0x5c, 0xff), // Bright Blue
        new Color(0xff, 0x00, 0xff), // Bright Magenta
        new Color(0x00, 0xff, 0xff), // Bright Cyan
        new Color(0xff, 0xff, 0xff), // Bright White
    };

    public HeadlessTerminalState(int cols = 80, int rows = 24)
    {
        _cols = cols;
        _rows = rows;
        _cells = new Cell[rows, cols];
        
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                _cells[row, col] = new Cell(" ", null, null, CellAttributes.None);
            }
        }
    }

    public Cell? GetCell(int col, int row)
    {
        if (col < 0 || col >= _cols || row < 0 || row >= _rows)
            return null;
        return _cells[row, col];
    }

    public void ProcessData(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var i = 0;
        
        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                i = ProcessEscapeSequence(text, i);
            }
            else if (text[i] == '\r')
            {
                _cursorCol = 0;
                i++;
            }
            else if (text[i] == '\n')
            {
                _cursorRow++;
                if (_cursorRow >= _rows)
                    _cursorRow = _rows - 1;
                i++;
            }
            else if (text[i] >= 32)
            {
                PutChar(text[i]);
                i++;
            }
            else
            {
                i++;
            }
        }
    }

    private void PutChar(char ch)
    {
        if (_cursorRow >= 0 && _cursorRow < _rows && _cursorCol >= 0 && _cursorCol < _cols)
        {
            _cells[_cursorRow, _cursorCol] = new Cell(
                ch.ToString(),
                _currentFg,
                _currentBg,
                _currentAttrs
            );
        }
        
        _cursorCol++;
        if (_cursorCol >= _cols)
        {
            _cursorCol = 0;
            _cursorRow++;
            if (_cursorRow >= _rows)
                _cursorRow = _rows - 1;
        }
    }

    private int ProcessEscapeSequence(string text, int start)
    {
        var i = start + 2;
        var args = new List<int>();
        var currentArg = new StringBuilder();

        while (i < text.Length)
        {
            if (char.IsDigit(text[i]))
            {
                currentArg.Append(text[i]);
                i++;
            }
            else if (text[i] == ';')
            {
                if (currentArg.Length > 0)
                {
                    args.Add(int.Parse(currentArg.ToString()));
                    currentArg.Clear();
                }
                else
                {
                    args.Add(0);
                }
                i++;
            }
            else if (char.IsLetter(text[i]))
            {
                if (currentArg.Length > 0)
                {
                    args.Add(int.Parse(currentArg.ToString()));
                }

                ProcessSGR(text[i], args);
                return i + 1;
            }
            else
            {
                return i + 1;
            }
        }

        return i;
    }

    private void ProcessSGR(char command, List<int> args)
    {
        if (command != 'm')
            return;

        if (args.Count == 0)
            args.Add(0);

        for (int i = 0; i < args.Count; i++)
        {
            var code = args[i];

            switch (code)
            {
                case 0:
                    _currentFg = null;
                    _currentBg = null;
                    _currentAttrs = CellAttributes.None;
                    break;
                case 1:
                    _currentAttrs |= CellAttributes.Bold;
                    break;
                case 2:
                    _currentAttrs |= CellAttributes.Dim;
                    break;
                case 3:
                    _currentAttrs |= CellAttributes.Italic;
                    break;
                case 4:
                    _currentAttrs |= CellAttributes.Underline;
                    break;
                case 7:
                    _currentAttrs |= CellAttributes.Inverse;
                    break;
                case 9:
                    _currentAttrs |= CellAttributes.Strikethrough;
                    break;
                case >= 30 and <= 37:
                    _currentFg = AnsiColors[code - 30];
                    break;
                case >= 40 and <= 47:
                    _currentBg = AnsiColors[code - 40];
                    break;
                case >= 90 and <= 97:
                    _currentFg = AnsiColors[code - 90 + 8];
                    break;
                case >= 100 and <= 107:
                    _currentBg = AnsiColors[code - 100 + 8];
                    break;
                case 38:
                    if (i + 1 < args.Count && args[i + 1] == 5 && i + 2 < args.Count)
                    {
                        _currentFg = Get256Color(args[i + 2]);
                        i += 2;
                    }
                    else if (i + 1 < args.Count && args[i + 1] == 2 && i + 4 < args.Count)
                    {
                        _currentFg = new Color((byte)args[i + 2], (byte)args[i + 3], (byte)args[i + 4]);
                        i += 4;
                    }
                    break;
                case 48:
                    if (i + 1 < args.Count && args[i + 1] == 5 && i + 2 < args.Count)
                    {
                        _currentBg = Get256Color(args[i + 2]);
                        i += 2;
                    }
                    else if (i + 1 < args.Count && args[i + 1] == 2 && i + 4 < args.Count)
                    {
                        _currentBg = new Color((byte)args[i + 2], (byte)args[i + 3], (byte)args[i + 4]);
                        i += 4;
                    }
                    break;
            }
        }
    }

    private Color Get256Color(int index)
    {
        if (index < 16)
            return AnsiColors[index];

        if (index < 232)
        {
            var i = index - 16;
            var r = (i / 36) * 51;
            var g = ((i % 36) / 6) * 51;
            var b = (i % 6) * 51;
            return new Color((byte)r, (byte)g, (byte)b);
        }

        var gray = 8 + (index - 232) * 10;
        return new Color((byte)gray, (byte)gray, (byte)gray);
    }
}
