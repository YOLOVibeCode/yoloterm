using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YOLOTerm.Core.Pty;

namespace YOLOTerm.App.Controls;

/// <summary>
/// Control representing a single terminal pane with metadata label bar
/// </summary>
public class PaneControl : UserControl
{
    private readonly Grid _layout;
    private readonly Border _labelBar;
    private readonly TextBlock _labelText;
    private readonly ContentPresenter _terminalHost;
    
    public IPtySession? Session { get; set; }
    
    public static readonly DependencyProperty PaneIdProperty =
        DependencyProperty.Register(
            nameof(PaneId),
            typeof(string),
            typeof(PaneControl),
            new PropertyMetadata(string.Empty));

    public string PaneId
    {
        get => (string)GetValue(PaneIdProperty);
        set => SetValue(PaneIdProperty, value);
    }

    public static readonly DependencyProperty MetadataTextProperty =
        DependencyProperty.Register(
            nameof(MetadataText),
            typeof(string),
            typeof(PaneControl),
            new PropertyMetadata("~", OnMetadataTextChanged));

    public string MetadataText
    {
        get => (string)GetValue(MetadataTextProperty);
        set => SetValue(MetadataTextProperty, value);
    }

    private static void OnMetadataTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PaneControl pane)
        {
            pane._labelText.Text = e.NewValue as string ?? "~";
        }
    }

    public PaneControl()
    {
        _layout = new Grid();
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) }); // Label bar
        _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Terminal

        // Label bar
        _labelBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 2, 8, 2)
        };

        _labelText = new TextBlock
        {
            Text = "~",
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        _labelBar.Child = _labelText;
        Grid.SetRow(_labelBar, 0);
        _layout.Children.Add(_labelBar);

        // Terminal host
        _terminalHost = new ContentPresenter();
        Grid.SetRow(_terminalHost, 1);
        _layout.Children.Add(_terminalHost);

        Content = _layout;
    }

    public void SetTerminalControl(UIElement terminalControl)
    {
        _terminalHost.Content = terminalControl;
    }

    public void UpdateMetadata(string cwd, string? gitBranch, string? shell, string? sshHost)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(cwd))
        {
            parts.Add(cwd);
        }

        if (!string.IsNullOrEmpty(gitBranch))
        {
            parts.Add($"⎇ {gitBranch}");
        }

        if (!string.IsNullOrEmpty(sshHost))
        {
            parts.Add($"SSH: {sshHost}");
        }

        if (!string.IsNullOrEmpty(shell))
        {
            parts.Add(shell);
        }

        MetadataText = parts.Count > 0 ? string.Join(" · ", parts) : "~";
    }
}
