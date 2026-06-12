namespace YOLOTerm.Core.Theme;

/// <summary>
/// Resolve current theme and emit change events (contracts v1)
/// Threading: Main thread only
/// </summary>
public interface IThemeSource
{
    /// <summary>
    /// Returns active theme
    /// </summary>
    Task<Theme> GetCurrentThemeAsync();

    /// <summary>
    /// Switch to theme by ID
    /// </summary>
    /// <exception cref="InvalidOperationException">Theme not found or JSON invalid</exception>
    Task LoadThemeAsync(string id);

    /// <summary>
    /// List all themes from contracts/themes/
    /// </summary>
    Task<List<Theme>> GetAvailableThemesAsync();

    /// <summary>
    /// Subscribe to theme changes
    /// Handler called on every surface when theme switches
    /// </summary>
    event Action<Theme>? OnThemeChanged;
}
