using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Contacts;
using System.IO;
using System.Text.Json;

namespace ConSecOrg.Client.Infrastructure.Local;

/// <summary>
/// Хранит контакты в JSON-файле %AppData%\ConSecOrg\{userId}\contacts.json
/// для персонального режима (без сервера).
/// </summary>
public sealed class LocalContactsService : IContactsApiService
{
    private readonly LocalUserStore _userStore;

    public LocalContactsService(LocalUserStore userStore)
    {
        _userStore = userStore;
    }

    private string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ConSecOrg",
                _userStore.UserId.ToString());
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "contacts.json");
        }
    }

    private List<ContactDto> Load()
    {
        var path = FilePath;
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<ContactDto>>(json) ?? [];
        }
        catch { return []; }
    }

    private void Save(List<ContactDto> contacts)
    {
        var json = JsonSerializer.Serialize(contacts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public Task<IReadOnlyList<ContactDto>> GetContactsAsync()
    {
        var contacts = Load();
        return Task.FromResult<IReadOnlyList<ContactDto>>(contacts
            .OrderByDescending(c => c.IsPinned)
            .ThenBy(c => c.Name)
            .ToList());
    }

    public Task<ContactDto> GetContactAsync(Guid id)
    {
        var contact = Load().FirstOrDefault(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Контакт {id} не найден.");
        return Task.FromResult(contact);
    }

    public Task<Guid> CreateContactAsync(CreateContactRequestDto request)
    {
        var contacts = Load();
        var newContact = new ContactDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Notes = request.Notes,
            LinkedUserId = request.LinkedUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        contacts.Add(newContact);
        Save(contacts);
        return Task.FromResult(newContact.Id);
    }

    public Task UpdateContactAsync(Guid id, UpdateContactRequestDto request)
    {
        var contacts = Load();
        var existing = contacts.FirstOrDefault(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Контакт {id} не найден.");
        existing.Name = request.Name;
        existing.Email = request.Email;
        existing.Phone = request.Phone;
        existing.Notes = request.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        Save(contacts);
        return Task.CompletedTask;
    }

    public Task DeleteContactAsync(Guid id)
    {
        var contacts = Load();
        contacts.RemoveAll(c => c.Id == id);
        Save(contacts);
        return Task.CompletedTask;
    }
}
