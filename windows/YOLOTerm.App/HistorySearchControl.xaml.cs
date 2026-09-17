using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.App;

public partial class HistorySearchControl : UserControl
{
    private readonly HistoryStore historyStore;
    private readonly string? paneId;
    private readonly ObservableCollection<HistoryCommandViewModel> results;
    private System.Threading.CancellationTokenSource? searchCancellation;
    
    public event EventHandler<HistoryStore.Command>? CommandSelected;
    public event EventHandler? CloseRequested;
    
    public HistorySearchControl(string? paneId = null)
    {
        InitializeComponent();
        
        this.paneId = paneId;
        this.historyStore = new HistoryStore();
        this.results = new ObservableCollection<HistoryCommandViewModel>();
        
        ResultsListBox.ItemsSource = results;
        
        // Focus search box
        Loaded += (s, e) => SearchBox.Focus();
    }
    
    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim();
        
        // Cancel previous search
        searchCancellation?.Cancel();
        searchCancellation = new System.Threading.CancellationTokenSource();
        
        if (string.IsNullOrWhiteSpace(query))
        {
            // Show recent commands instead
            await LoadRecentCommandsAsync();
            return;
        }
        
        try
        {
            // Debounce search
            await System.Threading.Tasks.Task.Delay(300, searchCancellation.Token);
            
            var options = new HistoryStore.SearchOptions
            {
                PaneId = paneId,
                Limit = 100,
                OnlyFailed = OnlyFailedCheckBox.IsChecked == true,
                OnlyFavorites = OnlyFavoritesCheckBox.IsChecked == true
            };
            
            List<HistoryStore.Command> commands = await historyStore.SearchAsync(query, options);
            
            results.Clear();
            foreach (var command in commands)
            {
                results.Add(new HistoryCommandViewModel(command));
            }
            
            ResultsCountLabel.Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")}";
            StatusLabel.Text = results.Count == 0 ? "No matching commands found" : "Use arrow keys to navigate, Enter to select";
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Search failed: {ex.Message}";
        }
    }
    
    private async System.Threading.Tasks.Task LoadRecentCommandsAsync()
    {
        try
        {
            var options = new HistoryStore.SearchOptions
            {
                PaneId = paneId,
                Limit = 100,
                OnlyFailed = OnlyFailedCheckBox.IsChecked == true,
                OnlyFavorites = OnlyFavoritesCheckBox.IsChecked == true
            };
            
            List<HistoryStore.Command> commands = await historyStore.RecentAsync(options);
            
            results.Clear();
            foreach (var command in commands)
            {
                results.Add(new HistoryCommandViewModel(command));
            }
            
            ResultsCountLabel.Text = $"{results.Count} recent";
            StatusLabel.Text = "Showing recent commands. Type to search.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Failed to load history: {ex.Message}";
        }
    }
    
    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        // Trigger search with current query and new filters
        SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, UndoAction.None));
    }
    
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SelectPrevious();
            }
            else
            {
                SelectNext();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (results.Count > 0)
            {
                ResultsListBox.Focus();
                ResultsListBox.SelectedIndex = 0;
            }
            e.Handled = true;
        }
    }
    
    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        SelectPrevious();
    }
    
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        SelectNext();
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void SelectPrevious()
    {
        if (results.Count == 0)
            return;
        
        int currentIndex = ResultsListBox.SelectedIndex;
        int newIndex = currentIndex <= 0 ? results.Count - 1 : currentIndex - 1;
        ResultsListBox.SelectedIndex = newIndex;
        ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
    }
    
    private void SelectNext()
    {
        if (results.Count == 0)
            return;
        
        int currentIndex = ResultsListBox.SelectedIndex;
        int newIndex = currentIndex >= results.Count - 1 ? 0 : currentIndex + 1;
        ResultsListBox.SelectedIndex = newIndex;
        ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
    }
    
    private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsListBox.SelectedItem is HistoryCommandViewModel viewModel)
        {
            StatusLabel.Text = viewModel.Command.CommandText;
        }
    }
    
    private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsListBox.SelectedItem is HistoryCommandViewModel viewModel)
        {
            CommandSelected?.Invoke(this, viewModel.Command);
        }
    }
}

public class HistoryCommandViewModel
{
    public HistoryStore.Command Command { get; }
    
    public string CommandText => Command.CommandText;
    public string Shell => Command.Shell;
    public string? Cwd => Command.Cwd;
    
    public string TimestampFormatted
    {
        get
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(Command.Timestamp);
            var now = DateTimeOffset.Now;
            var diff = now - timestamp;
            
            if (diff.TotalHours < 1)
                return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}d ago";
            
            return timestamp.ToString("MMM d");
        }
    }
    
    public string ExitCodeFormatted
    {
        get
        {
            if (Command.ExitCode == null)
                return "";
            
            return Command.ExitCode == 0 
                ? "✓" 
                : $"✗ {Command.ExitCode}";
        }
    }
    
    public string DurationFormatted
    {
        get
        {
            if (Command.DurationMs == null)
                return "";
            
            int ms = Command.DurationMs.Value;
            if (ms < 1000)
                return $"{ms}ms";
            if (ms < 60000)
                return $"{ms / 1000.0:F1}s";
            
            return $"{ms / 60000}m {(ms % 60000) / 1000}s";
        }
    }
    
    public HistoryCommandViewModel(HistoryStore.Command command)
    {
        Command = command;
    }
}
