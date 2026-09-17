using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore settingsStore;
    private readonly ShellPluginInstaller pluginInstaller;
    private SettingsStore.Settings currentSettings;
    private List<ShellPluginInstaller.ShellInfo>? detectedShells;
    
    public SettingsWindow()
    {
        InitializeComponent();
        
        settingsStore = new SettingsStore();
        pluginInstaller = new ShellPluginInstaller();
        currentSettings = new SettingsStore.Settings();
        
        InitializeControls();
        _ = LoadSettingsAsync();
    }
    
    private void InitializeControls()
    {
        // Appearance - Theme
        ThemeComboBox.Items.Add("Vivid");
        ThemeComboBox.Items.Add("Dark");
        ThemeComboBox.Items.Add("Light");
        ThemeComboBox.Items.Add("One Dark");
        ThemeComboBox.Items.Add("Solarized Dark");
        ThemeComboBox.Items.Add("Solarized Light");
        ThemeComboBox.SelectedIndex = 0;
        
        // Appearance - Font Family
        var fonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
        foreach (var font in fonts)
        {
            FontFamilyComboBox.Items.Add(font.Source);
        }
        FontFamilyComboBox.SelectedItem = "Cascadia Code";
        
        // Appearance - Font Size
        int[] fontSizes = { 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22, 24 };
        foreach (int size in fontSizes)
        {
            FontSizeComboBox.Items.Add(size);
        }
        FontSizeComboBox.SelectedItem = 12;
        
        // Appearance - Opacity
        OpacitySlider.ValueChanged += (s, e) =>
        {
            OpacityLabel.Text = $"{e.NewValue * 100:F0}%";
        };
        
        // Behavior - Default Shell
        DefaultShellComboBox.Items.Add("pwsh");
        DefaultShellComboBox.Items.Add("powershell");
        DefaultShellComboBox.Items.Add("cmd");
        DefaultShellComboBox.Items.Add("bash");
        DefaultShellComboBox.SelectedIndex = 0;
        
        // Behavior - Scrollback
        int[] scrollbackOptions = { 1000, 5000, 10000, 20000, 50000, 100000 };
        foreach (int lines in scrollbackOptions)
        {
            ScrollbackComboBox.Items.Add(lines);
        }
        ScrollbackComboBox.SelectedItem = 10000;
        
        // History - Retention
        HistoryRetentionComboBox.Items.Add("7 days");
        HistoryRetentionComboBox.Items.Add("30 days");
        HistoryRetentionComboBox.Items.Add("90 days");
        HistoryRetentionComboBox.Items.Add("1 year");
        HistoryRetentionComboBox.Items.Add("Forever");
        HistoryRetentionComboBox.SelectedIndex = 2; // 90 days
        
        // Advanced - Cursor Style
        CursorStyleComboBox.Items.Add("block");
        CursorStyleComboBox.Items.Add("underline");
        CursorStyleComboBox.Items.Add("bar");
        CursorStyleComboBox.SelectedIndex = 0;
    }
    
    private async System.Threading.Tasks.Task LoadSettingsAsync()
    {
        try
        {
            currentSettings = await settingsStore.LoadAsync();
            ApplySettingsToUI();
            await LoadShellsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ApplySettingsToUI()
    {
        // Appearance
        ThemeComboBox.SelectedItem = CapitalizeFirst(currentSettings.Theme);
        FontFamilyComboBox.SelectedItem = currentSettings.FontFamily;
        FontSizeComboBox.SelectedItem = currentSettings.FontSize;
        OpacitySlider.Value = currentSettings.Opacity;
        
        // Behavior
        DefaultShellComboBox.SelectedItem = currentSettings.DefaultShell;
        
        switch (currentSettings.StartupBehavior)
        {
            case "restore":
                StartupRestoreRadio.IsChecked = true;
                break;
            case "empty":
                StartupEmptyRadio.IsChecked = true;
                break;
            case "custom":
                StartupCustomRadio.IsChecked = true;
                break;
        }
        
        CustomStartupCommandTextBox.Text = currentSettings.CustomStartupCommand ?? string.Empty;
        
        switch (currentSettings.CloseWindowBehavior)
        {
            case "confirm":
                CloseConfirmRadio.IsChecked = true;
                break;
            case "close":
                CloseAlwaysRadio.IsChecked = true;
                break;
            case "minimize":
                CloseMinimizeRadio.IsChecked = true;
                break;
        }
        
        ScrollbackComboBox.SelectedItem = currentSettings.ScrollbackLines;
        
        // History
        HistoryEnabledCheckBox.IsChecked = currentSettings.HistoryEnabled;
        
        int retentionDays = currentSettings.HistoryRetentionDays;
        HistoryRetentionComboBox.SelectedIndex = retentionDays switch
        {
            7 => 0,
            30 => 1,
            90 => 2,
            365 => 3,
            _ => 4 // Forever
        };
        
        HistoryRedactionCheckBox.IsChecked = currentSettings.HistoryRedactionEnabled;
        HistorySyncCheckBox.IsChecked = currentSettings.HistorySyncAcrossPanes;
        
        // Shell Integration
        ShellIntegrationCheckBox.IsChecked = currentSettings.ShellIntegrationEnabled;
        
        // Advanced
        DebugLoggingCheckBox.IsChecked = currentSettings.DebugLogging;
        EnableBellCheckBox.IsChecked = currentSettings.EnableBell;
        CopyOnSelectCheckBox.IsChecked = currentSettings.CopyOnSelect;
        PasteOnRightClickCheckBox.IsChecked = currentSettings.PasteOnRightClick;
        TrimWhitespaceCheckBox.IsChecked = currentSettings.TrimTrailingWhitespace;
        CursorStyleComboBox.SelectedItem = currentSettings.CursorStyle;
        CursorBlinkCheckBox.IsChecked = currentSettings.CursorBlink;
    }
    
    private void ApplyUIToSettings()
    {
        // Appearance
        currentSettings.Theme = (ThemeComboBox.SelectedItem as string ?? "vivid").ToLower();
        currentSettings.FontFamily = FontFamilyComboBox.SelectedItem as string ?? "Cascadia Code";
        currentSettings.FontSize = (int)(FontSizeComboBox.SelectedItem ?? 12);
        currentSettings.Opacity = OpacitySlider.Value;
        
        // Behavior
        currentSettings.DefaultShell = DefaultShellComboBox.SelectedItem as string ?? "pwsh";
        
        if (StartupRestoreRadio.IsChecked == true)
            currentSettings.StartupBehavior = "restore";
        else if (StartupEmptyRadio.IsChecked == true)
            currentSettings.StartupBehavior = "empty";
        else if (StartupCustomRadio.IsChecked == true)
            currentSettings.StartupBehavior = "custom";
        
        currentSettings.CustomStartupCommand = string.IsNullOrWhiteSpace(CustomStartupCommandTextBox.Text) 
            ? null 
            : CustomStartupCommandTextBox.Text;
        
        if (CloseConfirmRadio.IsChecked == true)
            currentSettings.CloseWindowBehavior = "confirm";
        else if (CloseAlwaysRadio.IsChecked == true)
            currentSettings.CloseWindowBehavior = "close";
        else if (CloseMinimizeRadio.IsChecked == true)
            currentSettings.CloseWindowBehavior = "minimize";
        
        currentSettings.ScrollbackLines = (int)(ScrollbackComboBox.SelectedItem ?? 10000);
        
        // History
        currentSettings.HistoryEnabled = HistoryEnabledCheckBox.IsChecked == true;
        
        currentSettings.HistoryRetentionDays = HistoryRetentionComboBox.SelectedIndex switch
        {
            0 => 7,
            1 => 30,
            2 => 90,
            3 => 365,
            _ => int.MaxValue // Forever
        };
        
        currentSettings.HistoryRedactionEnabled = HistoryRedactionCheckBox.IsChecked == true;
        currentSettings.HistorySyncAcrossPanes = HistorySyncCheckBox.IsChecked == true;
        
        // Shell Integration
        currentSettings.ShellIntegrationEnabled = ShellIntegrationCheckBox.IsChecked == true;
        
        // Advanced
        currentSettings.DebugLogging = DebugLoggingCheckBox.IsChecked == true;
        currentSettings.EnableBell = EnableBellCheckBox.IsChecked == true;
        currentSettings.CopyOnSelect = CopyOnSelectCheckBox.IsChecked == true;
        currentSettings.PasteOnRightClick = PasteOnRightClickCheckBox.IsChecked == true;
        currentSettings.TrimTrailingWhitespace = TrimWhitespaceCheckBox.IsChecked == true;
        currentSettings.CursorStyle = CursorStyleComboBox.SelectedItem as string ?? "block";
        currentSettings.CursorBlink = CursorBlinkCheckBox.IsChecked == true;
    }
    
    private async System.Threading.Tasks.Task LoadShellsAsync()
    {
        try
        {
            detectedShells = await pluginInstaller.DetectShellsAsync();
            UpdateShellsList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to detect shells: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    
    private void UpdateShellsList()
    {
        ShellsListBox.Items.Clear();
        
        if (detectedShells == null || detectedShells.Count == 0)
        {
            ShellsListBox.Items.Add("No supported shells detected");
            return;
        }
        
        foreach (var shell in detectedShells)
        {
            string status = shell.IsInstalled ? "[Installed]" : "[Not Installed]";
            string item = $"{shell.Name} - {status}";
            ShellsListBox.Items.Add(item);
        }
    }
    
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyUIToSettings();
            await settingsStore.SaveAsync(currentSettings);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private async void InstallPluginButton_Click(object sender, RoutedEventArgs e)
    {
        int selectedIndex = ShellsListBox.SelectedIndex;
        if (selectedIndex < 0 || detectedShells == null || selectedIndex >= detectedShells.Count)
        {
            MessageBox.Show("Please select a shell to install the plugin.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var shell = detectedShells[selectedIndex];
        
        if (shell.IsInstalled)
        {
            MessageBox.Show($"Plugin is already installed for {shell.Name}.", "Already Installed", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        try
        {
            await pluginInstaller.InstallAsync(shell);
            MessageBox.Show($"Plugin installed successfully for {shell.Name}.\n\nPlease restart your shell for changes to take effect.", 
                "Installation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadShellsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install plugin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void UninstallPluginButton_Click(object sender, RoutedEventArgs e)
    {
        int selectedIndex = ShellsListBox.SelectedIndex;
        if (selectedIndex < 0 || detectedShells == null || selectedIndex >= detectedShells.Count)
        {
            MessageBox.Show("Please select a shell to uninstall the plugin.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var shell = detectedShells[selectedIndex];
        
        if (!shell.IsInstalled)
        {
            MessageBox.Show($"Plugin is not installed for {shell.Name}.", "Not Installed", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var result = MessageBox.Show($"Are you sure you want to uninstall the plugin for {shell.Name}?", 
            "Confirm Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await pluginInstaller.UninstallAsync(shell);
                MessageBox.Show($"Plugin uninstalled successfully for {shell.Name}.", 
                    "Uninstallation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadShellsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to uninstall plugin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void RefreshShellsButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadShellsAsync();
    }
    
    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all command history? This action cannot be undone.", 
            "Confirm Clear History", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                using var historyStore = new HistoryStore();
                await historyStore.DeleteOlderThanAsync(0); // Delete all
                MessageBox.Show("Command history cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        return char.ToUpper(input[0]) + input.Substring(1);
    }
}
