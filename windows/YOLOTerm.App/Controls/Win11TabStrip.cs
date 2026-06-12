using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YOLOTerm.App.Controls;

/// <summary>
/// Windows 11-style tab strip with rounded tabs
/// </summary>
public class Win11TabStrip : UserControl
{
    private readonly StackPanel _tabPanel;
    private readonly Button _addButton;
    private readonly List<TabItem> _tabs = new();
    private TabItem? _activeTab;

    public event Action<string>? TabSelected;
    public event Action<string>? TabClosed;
    public event Action? NewTabRequested;

    public Win11TabStrip()
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32))
        };

        _tabPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        _addButton = new Button
        {
            Content = "+",
            Width = 40,
            Height = 36,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(0),
            FontSize = 18,
            Margin = new Thickness(4, 0, 0, 0)
        };
        _addButton.Click += (s, e) => NewTabRequested?.Invoke();

        container.Children.Add(_tabPanel);
        container.Children.Add(_addButton);

        Content = container;
    }

    public string AddTab(string title, object? tabState = null)
    {
        var tabId = Guid.NewGuid().ToString();
        var tab = new TabItem(tabId, title, tabState);
        
        tab.Selected += () =>
        {
            SetActiveTab(tab);
            TabSelected?.Invoke(tab.Id);
        };
        
        tab.CloseRequested += () =>
        {
            RemoveTab(tab.Id);
            TabClosed?.Invoke(tab.Id);
        };

        _tabs.Add(tab);
        _tabPanel.Children.Add(tab.Root);

        if (_activeTab == null)
        {
            SetActiveTab(tab);
        }

        return tabId;
    }

    public void RemoveTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab == null) return;

        _tabs.Remove(tab);
        _tabPanel.Children.Remove(tab.Root);

        // If removing active tab, activate another
        if (_activeTab == tab)
        {
            _activeTab = null;
            if (_tabs.Count > 0)
            {
                SetActiveTab(_tabs[0]);
            }
        }
    }

    public void SetActiveTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            SetActiveTab(tab);
        }
    }

    private void SetActiveTab(TabItem tab)
    {
        if (_activeTab != null)
        {
            _activeTab.SetActive(false);
        }

        _activeTab = tab;
        _activeTab.SetActive(true);
    }

    public void UpdateTabTitle(string tabId, string title)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        tab?.SetTitle(title);
    }

    public object? GetTabState(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        return tab?.State;
    }

    public void SetTabState(string tabId, object? state)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.State = state;
        }
    }

    private class TabItem
    {
        public string Id { get; }
        public object? State { get; set; }
        public Border Root { get; }
        
        private readonly TextBlock _titleText;
        private readonly Button _closeButton;
        private bool _isActive;

        public event Action? Selected;
        public event Action? CloseRequested;

        public TabItem(string id, string title, object? state)
        {
            Id = id;
            State = state;

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12, 0, 8, 0)
            };

            _titleText = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12
            };

            _closeButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Padding = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            _closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                CloseRequested?.Invoke();
            };

            content.Children.Add(_titleText);
            content.Children.Add(_closeButton);

            Root = new Border
            {
                Child = content,
                Height = 36,
                MinWidth = 120,
                MaxWidth = 240,
                Background = new SolidColorBrush(Color.FromArgb(128, 50, 50, 50)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Margin = new Thickness(2, 4, 0, 0),
                Cursor = Cursors.Hand
            };

            Root.MouseEnter += (s, e) =>
            {
                _closeButton.Visibility = Visibility.Visible;
                if (!_isActive)
                {
                    Root.Background = new SolidColorBrush(Color.FromArgb(180, 50, 50, 50));
                }
            };

            Root.MouseLeave += (s, e) =>
            {
                _closeButton.Visibility = Visibility.Collapsed;
                if (!_isActive)
                {
                    Root.Background = new SolidColorBrush(Color.FromArgb(128, 50, 50, 50));
                }
            };

            Root.MouseLeftButtonDown += (s, e) =>
            {
                Selected?.Invoke();
            };

            UpdateAppearance();
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            UpdateAppearance();
        }

        public void SetTitle(string title)
        {
            _titleText.Text = title;
        }

        private void UpdateAppearance()
        {
            if (_isActive)
            {
                Root.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                _titleText.Foreground = Brushes.White;
                _closeButton.Foreground = Brushes.White;
            }
            else
            {
                Root.Background = new SolidColorBrush(Color.FromArgb(128, 50, 50, 50));
                _titleText.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                _closeButton.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            }
        }
    }
}
