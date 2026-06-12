using System.Windows.Input;

namespace YOLOTerm.App.Input;

/// <summary>
/// Windows keyboard chord mapping (SPEC §7.4)
/// Ctrl-based with selection-aware Ctrl+C
/// </summary>
public static class WindowsKeymap
{
    private static readonly Dictionary<(Key, ModifierKeys), YOLOTerm.Core.Input.KeymapAction> _keymap = new()
    {
        // Clipboard - Ctrl+C is handled specially (selection-aware)
        [(Key.C, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.Copy,
        [(Key.V, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.Paste,
        [(Key.C, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.CopyAlways,

        // Tabs
        [(Key.T, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.NewTab,
        [(Key.W, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.CloseTab,
        [(Key.Tab, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.NextTab,
        [(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.PrevTab,
        [(Key.D1, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab1,
        [(Key.D2, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab2,
        [(Key.D3, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab3,
        [(Key.D4, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab4,
        [(Key.D5, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab5,
        [(Key.D6, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab6,
        [(Key.D7, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab7,
        [(Key.D8, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab8,
        [(Key.D9, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.SelectTab9,

        // Panes
        [(Key.D, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.NewPaneRight,
        [(Key.E, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.NewPaneDown,
        [(Key.W, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.ClosePane,
        [(Key.Up, ModifierKeys.Alt)] = YOLOTerm.Core.Input.KeymapAction.FocusPaneUp,
        [(Key.Down, ModifierKeys.Alt)] = YOLOTerm.Core.Input.KeymapAction.FocusPaneDown,
        [(Key.Left, ModifierKeys.Alt)] = YOLOTerm.Core.Input.KeymapAction.FocusPaneLeft,
        [(Key.Right, ModifierKeys.Alt)] = YOLOTerm.Core.Input.KeymapAction.FocusPaneRight,
        [(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.ZoomPane,

        // Search
        [(Key.F, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.FindInTerminal,
        [(Key.R, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.HistorySearchPane,
        [(Key.R, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.HistorySearchGlobal,

        // View
        [(Key.OemPlus, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.FontBigger,
        [(Key.OemMinus, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.FontSmaller,
        [(Key.D0, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.FontReset,

        // System
        [(Key.P, ModifierKeys.Control | ModifierKeys.Shift)] = YOLOTerm.Core.Input.KeymapAction.CommandPalette,
        [(Key.OemComma, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.Settings,
        [(Key.Q, ModifierKeys.Control)] = YOLOTerm.Core.Input.KeymapAction.Quit,
    };

    public static bool TryGetAction(Key key, ModifierKeys modifiers, out YOLOTerm.Core.Input.KeymapAction action)
    {
        return _keymap.TryGetValue((key, modifiers), out action);
    }

    public static bool IsCtrlC(Key key, ModifierKeys modifiers)
    {
        return key == Key.C && modifiers == ModifierKeys.Control;
    }
}
