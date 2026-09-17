namespace YOLOTerm.App;

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Shell;
using YOLOTerm.Core.Integration;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private JumpListManager? _jumpListManager;
    private string? _initialDirectory;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Unhandled exception: {ex?.Message}\n\n{ex?.StackTrace}",
                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        // Initialize Jump List
        _jumpListManager = new JumpListManager();
        UpdateJumpList();
        
        // Parse command-line arguments
        ParseCommandLineArgs(e.Args);
        
        // Handle protocol URLs (yoloterm://open?dir=...)
        if (e.Args.Length > 0)
        {
            var arg = e.Args[0];
            if (arg.StartsWith("yoloterm://", StringComparison.OrdinalIgnoreCase))
            {
                var dir = ProtocolRegistration.ParseProtocolUrl(arg);
                if (dir != null)
                {
                    _initialDirectory = dir;
                    _jumpListManager?.AddRecentDirectory(dir);
                    UpdateJumpList();
                }
            }
        }
    }
    
    private void ParseCommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dir" && i + 1 < args.Length)
            {
                var dir = args[i + 1];
                if (Directory.Exists(dir))
                {
                    _initialDirectory = dir;
                    _jumpListManager?.AddRecentDirectory(dir);
                    UpdateJumpList();
                }
            }
            else if (args[i] == "--register-protocol")
            {
                ProtocolRegistration.RegisterProtocol();
                ExplorerContextMenu.RegisterContextMenu();
                Shutdown(0);
                return;
            }
            else if (args[i] == "--unregister")
            {
                ProtocolRegistration.UnregisterProtocol();
                ExplorerContextMenu.UnregisterContextMenu();
                Shutdown(0);
                return;
            }
        }
    }
    
    public string? GetInitialDirectory()
    {
        return _initialDirectory;
    }
    
    public JumpListManager? GetJumpListManager()
    {
        return _jumpListManager;
    }
    
    private void UpdateJumpList()
    {
        if (_jumpListManager == null) return;
        
        try
        {
            var jumpList = new JumpList();
            jumpList.ShowRecentCategory = false;
            jumpList.ShowFrequentCategory = false;
            
            // Add "New Terminal" task
            jumpList.JumpItems.Add(new JumpTask
            {
                Title = "New Terminal",
                Description = "Open a new terminal window",
                ApplicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
                IconResourcePath = System.Reflection.Assembly.GetExecutingAssembly().Location
            });
            
            // Add recent directories
            foreach (var dir in _jumpListManager.RecentDirectories.Take(10))
            {
                if (Directory.Exists(dir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(dirName))
                    {
                        dirName = dir; // Root directory
                    }
                    
                    jumpList.JumpItems.Add(new JumpTask
                    {
                        Title = dirName,
                        Description = dir,
                        ApplicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
                        Arguments = $"--dir \"{dir}\"",
                        IconResourcePath = System.Reflection.Assembly.GetExecutingAssembly().Location
                    });
                }
            }
            
            JumpList.SetJumpList(this, jumpList);
            jumpList.Apply();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to update Jump List: {ex.Message}");
        }
    }
}
