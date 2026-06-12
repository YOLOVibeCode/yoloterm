namespace YOLOTerm.Core.Theme;

/// <summary>
/// Theme source implementation loading from contracts/themes/ (contracts v1)
/// </summary>
public class ThemeSource : IThemeSource
{
    private readonly string _themesDirectory;
    private Theme? _currentTheme;
    private List<Theme>? _availableThemes;

    public event Action<Theme>? OnThemeChanged;

    public ThemeSource(string themesDirectory)
    {
        _themesDirectory = themesDirectory;
    }

    public async Task<Theme> GetCurrentThemeAsync()
    {
        if (_currentTheme == null)
        {
            var themes = await GetAvailableThemesAsync();
            _currentTheme = themes.FirstOrDefault(t => t.Id == "vivid")
                ?? themes.FirstOrDefault()
                ?? throw new InvalidOperationException("No themes available");
        }
        return _currentTheme;
    }

    public async Task LoadThemeAsync(string id)
    {
        var themes = await GetAvailableThemesAsync();
        var theme = themes.FirstOrDefault(t => t.Id == id)
            ?? throw new InvalidOperationException($"Theme not found: {id}");

        _currentTheme = theme;
        OnThemeChanged?.Invoke(theme);
    }

    public async Task<List<Theme>> GetAvailableThemesAsync()
    {
        if (_availableThemes == null)
        {
            await Task.Run(() =>
            {
                _availableThemes = Theme.LoadAll(_themesDirectory);
            });
        }
        return _availableThemes;
    }
}
