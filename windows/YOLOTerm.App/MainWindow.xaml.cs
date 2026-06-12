using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YOLOTerm.App.Controls;
using YOLOTerm.App.Input;
using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Input;
using YOLOTerm.Core.Layout;
using YOLOTerm.Core.Metadata;
using YOLOTerm.Core.Pty;
using YOLOTerm.Core.Theme;

namespace YOLOTerm.App;

/// <summary>
/// Main window with multi-tab, multi-pane support (M7: Track B Phase 2)
/// </summary>
public partial class MainWindow : Window
{
    private IThemeSource? _themeSource;
    private readonly string _contractsRoot;
    
    // M7 Components
    private Win11TabStrip? _tabStrip;
    private FindBar? _findBar;
    private readonly Dictionary<string, TabState> _tabs = new();
    private string? _activeTabId;

    private class TabState
    {
        public TilingPanel Panel { get; set; } = new();
        public List<PaneState> Panes { get; set; } = new();
        public LayoutPreset Preset { get; set; } = LayoutPreset.Auto;
        public string? ZoomedPane { get; set; }
    }

    private class PaneState
    {
        public string Id { get; set; } = "";
        public PaneControl Control { get; set; } = new();
        public IPtySession? Session { get; set; }
        public PaneMetadataProvider? MetadataProvider { get; set; }
    }

    public MainWindow()
    {
        InitializeComponent();
        _contractsRoot = FindContractsRoot();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            InitializeTheme();
            InitializeUI();
            CreateInitialTab();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void InitializeTheme()
    {
        var themesPath = Path.Combine(_contractsRoot, "themes");
        _themeSource = new ThemeSource(themesPath);
        
        var theme = _themeSource.GetCurrentThemeAsync().Result;
        Title = $"YOLOTerm - {theme.Name}";
    }

    private void InitializeUI()
    {
        // Tab strip
        _tabStrip = new Win11TabStrip();
        _tabStrip.TabSelected += OnTabSelected;
        _tabStrip.TabClosed += OnTabClosed;
        _tabStrip.NewTabRequested += OnNewTabRequested;
        TabStripContainer.Children.Add(_tabStrip);

        // Find bar
        _findBar = new FindBar();
        _findBar.SearchRequested += OnSearchRequested;
        _findBar.FindPrevious += OnFindPrevious;
        _findBar.FindNext += OnFindNext;
        _findBar.Closed += OnFindClosed;
        FindBarContainer.Children.Add(_findBar);
    }

    private void CreateInitialTab()
    {
        var tabId = CreateTab("Terminal 1");
        CreatePaneInTab(tabId);
    }

    private string CreateTab(string title)
    {
        var tabState = new TabState();
        var tabId = _tabStrip!.AddTab(title, tabState);
        
        _tabs[tabId] = tabState;
        
        return tabId;
    }

    private void CreatePaneInTab(string tabId)
    {
        if (!_tabs.TryGetValue(tabId, out var tab)) return;

        var paneId = Guid.NewGuid().ToString();
        var paneControl = new PaneControl { PaneId = paneId, Name = paneId };
        
        var paneState = new PaneState
        {
            Id = paneId,
            Control = paneControl
        };

        // Launch terminal
        try
        {
            var shell = FindPowerShell();
            var cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var envPolicyPath = Path.Combine(_contractsRoot, "fixtures", "env-policy.json");
            var envPolicy = EnvPolicy.Load(envPolicyPath);

            var parentEnv = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? string.Empty);

            var env = envPolicy.Apply(parentEnv, "0.1.0");

            var spawner = new ConPtySpawner();
            var session = spawner.Spawn(shell, cwd, 80, 24, env);
            
            paneState.Session = session;
            
            session.OnExit += exitCode =>
            {
                Dispatcher.Invoke(() => RemovePane(tabId, paneId));
            };

            // Metadata provider
            var metadataProvider = new PaneMetadataProvider(session.Pid);
            metadataProvider.MetadataChanged += (s, metadata) =>
            {
                Dispatcher.Invoke(() =>
                {
                    paneControl.UpdateMetadata(
                        metadata.Cwd ?? cwd,
                        metadata.GitBranch,
                        metadata.Shell,
                        metadata.SshHost);
                });
            };
            paneState.MetadataProvider = metadataProvider;

            // Add terminal control (placeholder for now - will integrate Windows Terminal control)
            var placeholder = new Border
            {
                Background = System.Windows.Media.Brushes.Black,
                Child = new TextBlock
                {
                    Text = $"Terminal Pane {paneId.Substring(0, 8)}",
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            paneControl.SetTerminalControl(placeholder);

            tab.Panes.Add(paneState);
            tab.Panel.Children.Add(paneControl);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create pane: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemovePane(string tabId, string paneId)
    {
        if (!_tabs.TryGetValue(tabId, out var tab)) return;

        var pane = tab.Panes.FirstOrDefault(p => p.Id == paneId);
        if (pane == null) return;

        pane.Session?.Kill();
        pane.MetadataProvider?.Dispose();
        
        tab.Panes.Remove(pane);
        tab.Panel.Children.Remove(pane.Control);

        // If no panes left, close tab
        if (tab.Panes.Count == 0)
        {
            _tabStrip?.RemoveTab(tabId);
        }
    }

    private void OnTabSelected(string tabId)
    {
        if (_activeTabId == tabId) return;

        // Hide current tab's panel
        if (_activeTabId != null && _tabs.TryGetValue(_activeTabId, out var oldTab))
        {
            ContentContainer.Children.Remove(oldTab.Panel);
        }

        // Show new tab's panel
        if (_tabs.TryGetValue(tabId, out var newTab))
        {
            ContentContainer.Children.Add(newTab.Panel);
            _activeTabId = tabId;
        }
    }

    private void OnTabClosed(string tabId)
    {
        if (!_tabs.TryGetValue(tabId, out var tab)) return;

        // Cleanup all panes
        foreach (var pane in tab.Panes)
        {
            try { pane.Session?.Kill(); } catch { }
            pane.MetadataProvider?.Dispose();
        }

        ContentContainer.Children.Remove(tab.Panel);
        _tabs.Remove(tabId);

        // If no tabs left, create a new one
        if (_tabs.Count == 0)
        {
            CreateInitialTab();
        }
    }

    private void OnNewTabRequested()
    {
        var tabId = CreateTab($"Terminal {_tabs.Count + 1}");
        CreatePaneInTab(tabId);
        _tabStrip?.SetActiveTab(tabId);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key;
        var modifiers = Keyboard.Modifiers;

        // Selection-aware Ctrl+C
        if (WindowsKeymap.IsCtrlC(key, modifiers))
        {
            HandleCtrlC();
            e.Handled = true;
            return;
        }

        // Check for keymap actions
        if (WindowsKeymap.TryGetAction(key, modifiers, out var action))
        {
            HandleKeymapAction(action);
            e.Handled = true;
        }
    }

    private void HandleCtrlC()
    {
        // TODO: Check if terminal has selection
        // If selection exists: Copy
        // If no selection: Send SIGINT to PTY
        
        // For now, just demonstrate the concept
        MessageBox.Show("Ctrl+C - selection-aware behavior", "Keymap", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void HandleKeymapAction(KeymapAction action)
    {
        switch (action)
        {
            case KeymapAction.NewTab:
                OnNewTabRequested();
                break;
            
            case KeymapAction.CloseTab:
                if (_activeTabId != null)
                {
                    _tabStrip?.RemoveTab(_activeTabId);
                }
                break;

            case KeymapAction.NewPaneRight:
                if (_activeTabId != null)
                {
                    CreatePaneInTab(_activeTabId);
                    if (_tabs.TryGetValue(_activeTabId, out var tab))
                    {
                        tab.Preset = LayoutPreset.Columns;
                        tab.Panel.Preset = LayoutPreset.Columns;
                    }
                }
                break;

            case KeymapAction.NewPaneDown:
                if (_activeTabId != null)
                {
                    CreatePaneInTab(_activeTabId);
                    if (_tabs.TryGetValue(_activeTabId, out var tab))
                    {
                        tab.Preset = LayoutPreset.Rows;
                        tab.Panel.Preset = LayoutPreset.Rows;
                    }
                }
                break;

            case KeymapAction.FindInTerminal:
                _findBar?.Show();
                break;

            case KeymapAction.Quit:
                Close();
                break;

            default:
                MessageBox.Show($"Action: {action}", "Keymap", MessageBoxButton.OK, MessageBoxImage.Information);
                break;
        }
    }

    private void OnSearchRequested(string query)
    {
        // TODO: Search in active terminal buffer
        _findBar?.UpdateResults(0, 0);
    }

    private void OnFindPrevious()
    {
        // TODO: Navigate to previous match
    }

    private void OnFindNext()
    {
        // TODO: Navigate to next match
    }

    private void OnFindClosed()
    {
        // Focus back to terminal
    }

    private string FindPowerShell()
    {
        var pwshPaths = new[]
        {
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            @"C:\Program Files (x86)\PowerShell\7\pwsh.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
        };

        foreach (var path in pwshPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return "powershell.exe";
    }

    private string FindContractsRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        
        while (dir != null)
        {
            var contractsPath = Path.Combine(dir.FullName, "contracts");
            if (Directory.Exists(contractsPath))
                return contractsPath;

            if (dir.Parent == null)
                break;
            
            dir = dir.Parent;
        }

        var devPath = Path.Combine(dir?.FullName ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "contracts");
        if (Directory.Exists(devPath))
            return Path.GetFullPath(devPath);

        throw new InvalidOperationException("Could not find contracts directory");
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var tab in _tabs.Values)
        {
            foreach (var pane in tab.Panes)
            {
                try { pane.Session?.Kill(); } catch { }
                pane.MetadataProvider?.Dispose();
            }
        }
        
        base.OnClosed(e);
    }
}
