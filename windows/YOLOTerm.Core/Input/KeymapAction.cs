namespace YOLOTerm.Core.Input;

/// <summary>
/// Semantic keymap actions (contracts v1)
/// </summary>
public enum KeymapAction
{
    // Clipboard
    Copy,
    Paste,
    CopyAlways,

    // Tabs
    NewTab,
    CloseTab,
    NextTab,
    PrevTab,
    SelectTab1,
    SelectTab2,
    SelectTab3,
    SelectTab4,
    SelectTab5,
    SelectTab6,
    SelectTab7,
    SelectTab8,
    SelectTab9,

    // Panes
    NewPaneRight,
    NewPaneDown,
    ClosePane,
    FocusPaneUp,
    FocusPaneDown,
    FocusPaneLeft,
    FocusPaneRight,
    SelectPane1,
    SelectPane2,
    SelectPane3,
    SelectPane4,
    SelectPane5,
    SelectPane6,
    SelectPane7,
    SelectPane8,
    SelectPane9,
    ZoomPane,

    // Search
    FindInTerminal,
    HistorySearchPane,
    HistorySearchGlobal,

    // View
    FontBigger,
    FontSmaller,
    FontReset,

    // System
    CommandPalette,
    Settings,
    Quit
}
