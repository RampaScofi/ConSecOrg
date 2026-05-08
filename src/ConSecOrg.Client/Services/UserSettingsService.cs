using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConSecOrg.Client.Services;

public sealed class UserSettings
{
    [JsonPropertyName("theme")] public string Theme { get; set; } = "Dark";
    [JsonPropertyName("accentHex")] public string AccentHex { get; set; } = "#6C63FF";
    [JsonPropertyName("secondaryAccentHex")] public string SecondaryAccentHex { get; set; } = "#00BCD4";
    [JsonPropertyName("fontSize")] public int FontSize { get; set; } = 14;
    [JsonPropertyName("backgroundImagePath")] public string BackgroundImagePath { get; set; } = string.Empty;
    [JsonPropertyName("backgroundOverlayOpacity")] public double BackgroundOverlayOpacity { get; set; } = 0.88;
    [JsonPropertyName("notesPin")] public string? NotesPinSalt { get; set; }
    [JsonPropertyName("notesPinVerifier")] public string? NotesPinVerifier { get; set; }
}

/// <summary>
/// Управление пользовательскими настройками (тема, фон, NotesPIN) с раздельным хранением для каждого пользователя.
/// Файл: %AppData%\ConSecOrg\users\{userId}\settings.json
/// </summary>
public sealed class UserSettingsService
{
    private static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ConSecOrg", "users");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private string? _userDir;
    private UserSettings _current = new();

    public UserSettings Current => _current;
    public string? CurrentUserId { get; private set; }
    public event Action? Changed;

    /// <summary>Загрузить настройки пользователя по его ID. Если файла нет — вернуть значения по умолчанию.</summary>
    public UserSettings LoadFor(Guid userId)
    {
        CurrentUserId = userId.ToString();
        _userDir = Path.Combine(Root, CurrentUserId);
        Directory.CreateDirectory(_userDir);

        var path = Path.Combine(_userDir, "settings.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                _current = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch { _current = new UserSettings(); }
        }
        else
        {
            _current = new UserSettings();
        }

        return _current;
    }

    /// <summary>Сохранить текущие настройки на диск.</summary>
    public void Save()
    {
        if (_userDir == null) return;
        Directory.CreateDirectory(_userDir);
        var path = Path.Combine(_userDir, "settings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(_current, JsonOpts));
        Changed?.Invoke();
    }

    /// <summary>Сохранить копию загруженного фонового изображения внутри папки пользователя
    /// (чтобы при удалении исходного файла фон не пропал).</summary>
    public string CopyBackgroundImage(string sourcePath)
    {
        if (_userDir == null || !File.Exists(sourcePath)) return sourcePath;
        var ext = Path.GetExtension(sourcePath);
        var dest = Path.Combine(_userDir, $"bg{ext}");
        try
        {
            File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            return sourcePath;
        }
    }

    public void UpdateTheme(string theme, string accentHex, int fontSize,
                             string backgroundImagePath, double backgroundOverlayOpacity,
                             string secondaryAccentHex = "#00BCD4")
    {
        _current.Theme = theme;
        _current.AccentHex = accentHex;
        _current.SecondaryAccentHex = secondaryAccentHex;
        _current.FontSize = fontSize;
        _current.BackgroundImagePath = backgroundImagePath;
        _current.BackgroundOverlayOpacity = backgroundOverlayOpacity;
        Save();
    }

    /// <summary>Сбрасывает настройки (для logout-а).</summary>
    public void Clear()
    {
        _userDir = null;
        CurrentUserId = null;
        _current = new UserSettings();
    }
}
