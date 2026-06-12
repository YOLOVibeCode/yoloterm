using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace YOLOTerm.App;

using System.Windows;
using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Pty;
using YOLOTerm.Core.Theme;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private IPtySession? _ptySession;
    private IThemeSource? _themeSource;
    private readonly string _contractsRoot;

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
            LaunchTerminal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize terminal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void LaunchTerminal()
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
        _ptySession = spawner.Spawn(shell, cwd, 80, 24);

        _ptySession.OnExit += exitCode =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Shell exited with code {exitCode}", "Terminal Closed", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            });
        };

        StartReadingOutput();
    }

    private async void StartReadingOutput()
    {
        if (_ptySession == null) return;

        await Task.Run(() =>
        {
            try
            {
                while (_ptySession.IsAlive)
                {
                    Thread.Sleep(100);
                }
            }
            catch
            {
            }
        });
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_ptySession != null && _ptySession.IsAlive)
        {
            try
            {
                var cols = Math.Max(10, (int)(ActualWidth / 9.0));
                var rows = Math.Max(3, (int)(ActualHeight / 18.0));
                _ptySession.Resize(cols, rows);
            }
            catch
            {
            }
        }
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
        _ptySession?.Dispose();
        base.OnClosed(e);
    }
}