using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace YOLOTerm.Core.Integration;

/// <summary>
/// Handles yoloterm:// URL protocol registration with Windows.
/// Format: yoloterm://open?dir=C:\Path\To\Folder
/// </summary>
public static class ProtocolRegistration
{
    private const string ProtocolScheme = "yoloterm";
    private const string ProtocolDescription = "URL:YOLOTerm Protocol";
    
    /// <summary>
    /// Registers the yoloterm:// protocol with Windows registry.
    /// Requires administrator privileges.
    /// </summary>
    /// <param name="exePath">Full path to YOLOTerm.exe</param>
    /// <returns>True if registration succeeded, false if insufficient privileges or error</returns>
    public static bool RegisterProtocol(string? exePath = null)
    {
        try
        {
            // Get executable path
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Assembly.GetExecutingAssembly().Location;
                
                // If running as DLL (dotnet publish), find the .exe
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var directory = Path.GetDirectoryName(exePath);
                    var exeName = Path.GetFileNameWithoutExtension(exePath) + ".exe";
                    exePath = Path.Combine(directory ?? "", exeName);
                }
            }
            
            if (!File.Exists(exePath))
            {
                Console.Error.WriteLine($"ERROR: Executable not found: {exePath}");
                return false;
            }
            
            // Register protocol in HKEY_CURRENT_USER (no admin required)
            using var key = Registry.CurrentUser.CreateSubKey(@$"Software\Classes\{ProtocolScheme}");
            key.SetValue("", ProtocolDescription);
            key.SetValue("URL Protocol", "");
            
            // Set default icon
            using var iconKey = key.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", $"\"{exePath}\",0");
            
            // Set command to launch
            using var commandKey = key.CreateSubKey(@"shell\open\command");
            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
            
            Console.WriteLine($"✓ Registered yoloterm:// protocol handler");
            Console.WriteLine($"  Executable: {exePath}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("ERROR: Insufficient permissions to register protocol. Run as administrator.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to register protocol: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Unregisters the yoloterm:// protocol from Windows registry.
    /// </summary>
    /// <returns>True if unregistration succeeded</returns>
    public static bool UnregisterProtocol()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@$"Software\Classes\{ProtocolScheme}", false);
            Console.WriteLine($"✓ Unregistered yoloterm:// protocol handler");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to unregister protocol: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Checks if the protocol is currently registered.
    /// </summary>
    public static bool IsProtocolRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@$"Software\Classes\{ProtocolScheme}");
            return key != null;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Parses a yoloterm:// URL and extracts the directory parameter.
    /// </summary>
    /// <param name="url">URL string (e.g., yoloterm://open?dir=C:\Path)</param>
    /// <returns>Directory path, or null if invalid</returns>
    public static string? ParseProtocolUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith($"{ProtocolScheme}://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        
        try
        {
            var uri = new Uri(url);
            
            // Check for open action
            if (uri.Host.Equals("open", StringComparison.OrdinalIgnoreCase))
            {
                // Parse query string
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var dir = query["dir"];
                
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }
        }
        catch
        {
            return null;
        }
        
        return null;
    }
}
