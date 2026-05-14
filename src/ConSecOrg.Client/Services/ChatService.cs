using ConSecOrg.Client.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ConSecOrg.Client.Services;

public sealed class ChatService
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private static string ChatDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConSecOrg", "chats");

    private static string FilePath(string chatId) =>
        Path.Combine(ChatDir, $"chat_{chatId}.json");

    private static string FilesDir(string chatId) =>
        Path.Combine(ChatDir, chatId, "files");

    public event Action? MessagesChanged;

    public List<ChatMessageEntry> LoadMessages(string chatId)
    {
        try
        {
            var path = FilePath(chatId);
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<ChatMessageEntry>>(File.ReadAllText(path), _json) ?? [];
        }
        catch { return []; }
    }

    public ChatMessageEntry AddMessage(string chatId, string senderUserId, string senderUsername,
        string text, string? sourceFilePath = null)
    {
        string? attachmentPath = null;
        string? attachmentFileName = null;

        if (!string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath))
        {
            var filesDir = FilesDir(chatId);
            Directory.CreateDirectory(filesDir);
            attachmentFileName = Path.GetFileName(sourceFilePath);
            var dest = Path.Combine(filesDir, $"{Guid.NewGuid()}_{attachmentFileName}");
            File.Copy(sourceFilePath, dest);
            attachmentPath = dest;
        }

        var msg = new ChatMessageEntry
        {
            SenderUserId = senderUserId,
            SenderUsername = senderUsername,
            Text = text,
            SentAt = DateTime.Now,
            AttachmentPath = attachmentPath,
            AttachmentFileName = attachmentFileName
        };
        var messages = LoadMessages(chatId);
        messages.Add(msg);
        Save(chatId, messages);
        MessagesChanged?.Invoke();
        return msg;
    }

    private void Save(string chatId, List<ChatMessageEntry> messages)
    {
        Directory.CreateDirectory(ChatDir);
        File.WriteAllText(FilePath(chatId), JsonSerializer.Serialize(messages, _json));
    }
}
