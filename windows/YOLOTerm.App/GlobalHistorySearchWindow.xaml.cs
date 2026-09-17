using System;
using System.Windows;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.App;

public partial class GlobalHistorySearchWindow : Window
{
    public HistoryStore.Command? SelectedCommand { get; private set; }
    
    public GlobalHistorySearchWindow()
    {
        InitializeComponent();
    }
    
    private void SearchControl_CommandSelected(object? sender, HistoryStore.Command command)
    {
        SelectedCommand = command;
        DialogResult = true;
        Close();
    }
    
    private void SearchControl_CloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
