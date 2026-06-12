using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YOLOTerm.App.Controls;

/// <summary>
/// Find UI for searching terminal buffer
/// </summary>
public class FindBar : UserControl
{
    private readonly TextBox _searchBox;
    private readonly TextBlock _resultText;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _closeButton;

    public event Action<string>? SearchRequested;
    public event Action? FindPrevious;
    public event Action? FindNext;
    public event Action? Closed;

    public FindBar()
    {
        var container = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        // Search box
        _searchBox = new TextBox
        {
            Width = 200,
            Height = 24,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2)
        };

        _searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                var query = _searchBox.Text;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    SearchRequested?.Invoke(query);
                }
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        _searchBox.TextChanged += (s, e) =>
        {
            var query = _searchBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
            {
                SearchRequested?.Invoke(query);
            }
        };

        // Result text
        _resultText = new TextBlock
        {
            Text = "",
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            FontSize = 11
        };

        // Previous button
        _previousButton = CreateButton("↑", "Previous match (Shift+Enter)");
        _previousButton.Click += (s, e) => FindPrevious?.Invoke();

        // Next button
        _nextButton = CreateButton("↓", "Next match (Enter)");
        _nextButton.Click += (s, e) => FindNext?.Invoke();

        // Close button
        _closeButton = CreateButton("×", "Close (Esc)");
        _closeButton.Click += (s, e) => Close();

        panel.Children.Add(_searchBox);
        panel.Children.Add(_resultText);
        panel.Children.Add(_previousButton);
        panel.Children.Add(_nextButton);
        panel.Children.Add(_closeButton);

        container.Child = panel;
        Content = container;

        Visibility = Visibility.Collapsed;
    }

    private Button CreateButton(string content, string tooltip)
    {
        return new Button
        {
            Content = content,
            Width = 24,
            Height = 24,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(0),
            FontSize = 14,
            Margin = new Thickness(2, 0, 2, 0),
            ToolTip = tooltip
        };
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        _searchBox.Focus();
        _searchBox.SelectAll();
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        Closed?.Invoke();
    }

    public void UpdateResults(int currentIndex, int totalMatches)
    {
        if (totalMatches == 0)
        {
            _resultText.Text = "No matches";
        }
        else
        {
            _resultText.Text = $"{currentIndex + 1} of {totalMatches}";
        }
    }

    public void ClearResults()
    {
        _resultText.Text = "";
    }

    public string Query => _searchBox.Text;
}
