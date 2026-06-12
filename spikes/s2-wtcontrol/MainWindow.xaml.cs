using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WTControlSpike;

public partial class MainWindow : Window
{
    private bool _screenshotMode = false;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Check for screenshot mode (CI environment)
        var args = Environment.GetCommandLineArgs();
        _screenshotMode = args.Length > 1 && args[1] == "--screenshot";
    }
    
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine("Initializing terminal...");
            
            // Start PowerShell
            Terminal.StartTerminal("pwsh.exe", "");
            
            Console.WriteLine("Terminal started successfully");
            
            // If in screenshot mode, run tests and capture
            if (_screenshotMode)
            {
                await Task.Delay(2000); // Wait for shell to initialize
                await RunColorBatteryAndScreenshot();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing terminal: {ex.Message}");
            MessageBox.Show($"Failed to initialize terminal: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async Task RunColorBatteryAndScreenshot()
    {
        try
        {
            Console.WriteLine("Running color battery...");
            
            // Send color test commands
            var commands = new[]
            {
                "# Windows Terminal Control Color Battery",
                "Write-Host \"Test 1: ANSI-16 Foreground Colors\" -ForegroundColor White",
                "Write-Host \"Black\" -ForegroundColor Black -BackgroundColor White",
                "Write-Host \"Red\" -ForegroundColor Red",
                "Write-Host \"Green\" -ForegroundColor Green",
                "Write-Host \"Yellow\" -ForegroundColor Yellow",
                "Write-Host \"Blue\" -ForegroundColor Blue",
                "Write-Host \"Magenta\" -ForegroundColor Magenta",
                "Write-Host \"Cyan\" -ForegroundColor Cyan",
                "Write-Host \"White\" -ForegroundColor White",
                "",
                "Write-Host \"Test 2: Background Colors\" -ForegroundColor White",
                "Write-Host \"   \" -BackgroundColor Red",
                "Write-Host \"   \" -BackgroundColor Green",
                "Write-Host \"   \" -BackgroundColor Blue",
                "",
                "Write-Host \"Test 3: ANSI Escape Sequences\"",
                "Write-Host \"`e[31mRed via ANSI`e[0m\"",
                "Write-Host \"`e[32mGreen via ANSI`e[0m\"",
                "Write-Host \"`e[34mBlue via ANSI`e[0m\"",
                "Write-Host \"`e[1;33mBold Yellow`e[0m\"",
                "",
                "Write-Host \"Test 4: 256-color (sample)\"",
                "0..15 | ForEach-Object { Write-Host -NoNewline \"`e[38;5;${_}m█`e[0m\" }",
                "Write-Host \"\"",
                "",
                "Write-Host \"Color battery complete. Close window to capture screenshot.\"",
            };
            
            foreach (var cmd in commands)
            {
                Terminal.WriteLine(cmd);
                await Task.Delay(200); // Let each command render
            }
            
            // Wait a bit more for rendering to complete
            await Task.Delay(2000);
            
            // Capture screenshot
            SaveScreenshot("artifacts/screenshot-colorbattery.png");
            
            Console.WriteLine("Screenshot saved. Closing in 2 seconds...");
            await Task.Delay(2000);
            
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during color battery: {ex.Message}");
            Application.Current.Shutdown(1);
        }
    }
    
    private void SaveScreenshot(string filename)
    {
        try
        {
            var renderTarget = new RenderTargetBitmap(
                (int)ActualWidth,
                (int)ActualHeight,
                96, 96,
                PixelFormats.Pbgra32);
            
            renderTarget.Render(this);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            
            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "artifacts");
            using var stream = File.Create(filename);
            encoder.Save(stream);
            
            Console.WriteLine($"Screenshot saved to: {filename}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save screenshot: {ex.Message}");
        }
    }
}
