using ConSecOrg.Client.Infrastructure.Api;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace ConSecOrg.Client.Services;

/// <summary>
/// Кэширует аватарки пользователей: запрашивает с сервера один раз, затем отдаёт из памяти.
/// В персональном режиме всегда возвращает null (аватарки только локальные).
/// </summary>
public sealed class AvatarCacheService(IUserSearchApiService api, ModeService mode)
{
    private readonly ConcurrentDictionary<Guid, string?> _cache = new();

    /// <summary>Вернуть base64 аватарки пользователя (или null если нет). Кэширует результат.</summary>
    public async Task<string?> GetAsync(Guid userId)
    {
        if (mode.IsPersonal) return null;
        if (_cache.TryGetValue(userId, out var cached)) return cached;

        var base64 = await api.GetUserAvatarAsync(userId);
        _cache[userId] = base64;
        return base64;
    }

    /// <summary>Обновить кэш после загрузки новой аватарки.</summary>
    public void Update(Guid userId, string? base64) => _cache[userId] = base64;

    /// <summary>Сбросить весь кэш (при logout).</summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Преобразовать base64-строку аватарки в BitmapImage для WPF-биндинга.
    /// Возвращает null если строка пустая.
    /// </summary>
    public static BitmapImage? Base64ToImage(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
