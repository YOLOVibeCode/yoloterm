using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace YOLOTerm.Core.Integration;

/// <summary>
/// Handles Windows Explorer context menu integration.
/// Adds "Open YOLOTerm Here" to folder right-click menu.
/// </summary>
public static class ExplorerContextMenu
{
    private const string MenuItemName = "YOLOTerm";
    private const string MenuItemText = "Open YOLOTerm Here";
    
    /// <summary>
    /// Registers "Open YOLOTerm Here" in Explorer context menu.
    /// Appears on folder right-click and directory background right-click.
    /// </summary>
    /// <param name="exePath">Full path to YOLOTerm.exe</param>
    /// <returns>True if registration succeeded</returns>
    public static bool RegisterContextMenu(string? exePath = null)
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
            
            // Register for directory background (right-click in empty space)
            RegisterForDirectoryBackground(exePath);
            
            // Register for directory (right-click on folder)
            RegisterForDirectory(exePath);
            
            Console.WriteLine($"✓ Registered Explorer context menu integration");
            Console.WriteLine($"  Menu item: {MenuItemText}");
            Console.WriteLine($"  Executable: {exePath}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("ERROR: Insufficient permissions to register context menu. Run as administrator.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to register context menu: {ex.Message}");
            return false;
        }
    }
    
    private static void RegisterForDirectoryBackground(string exePath)
    {
        // HKEY_CURRENT_USER\Software\Classes\Directory\Background\shell\YOLOTerm
        var keyPath = @"Software\Classes\Directory\Background\shell\" + MenuItemName;
        
        using var shellKey = Registry.CurrentUser.CreateSubKey(keyPath);
        shellKey.SetValue("", MenuItemText);
        shellKey.SetValue("Icon", exePath);
        
        using var commandKey = shellKey.CreateSubKey("command");
        commandKey.SetValue("", $"\"{exePath}\" --dir \"%V\"");
    }
    
    private static void RegisterForDirectory(string exePath)
    {
        // HKEY_CURRENT_USER\Software\Classes\Directory\shell\YOLOTerm
        var keyPath = @"Software\Classes\Directory\shell\" + MenuItemName;
        
        using var shellKey = Registry.CurrentUser.CreateSubKey(keyPath);
        shellKey.SetValue("", MenuItemText);
        shellKey.SetValue("Icon", exePath);
        
        using var commandKey = shellKey.CreateSubKey("command");
        commandKey.SetValue("", $"\"{exePath}\" --dir \"%1\"");
    }
    
    /// <summary>
    /// Unregisters Explorer context menu integration.
    /// </summary>
    public static bool UnregisterContextMenu()
    {
        try
        {
            // Remove directory background entry
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\" + MenuItemName, false);
            
            // Remove directory entry
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\" + MenuItemName, false);
            
            Console.WriteLine($"✓ Unregistered Explorer context menu integration");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to unregister context menu: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Checks if context menu is currently registered.
    /// </summary>
    public static bool IsContextMenuRegistered()
    {
        try
        {
            using var key1 = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\" + MenuItemName);
            using var key2 = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\" + MenuItemName);
            return key1 != null || key2 != null;
        }
        catch
        {
            return false;
        }
    }
}
