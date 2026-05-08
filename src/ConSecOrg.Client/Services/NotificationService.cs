using System.Windows;

namespace ConSecOrg.Client.Services;

public sealed class NotificationService
{
    public event Action<string, string, NotificationLevel>? NotificationRequested;

    // Raised when a note is destroyed by the destruction timer; subscribers can remove it from their lists.
    public event Action<Guid, string>? NoteTimerExpired;

    public void Info(string title, string message) =>
        NotificationRequested?.Invoke(title, message, NotificationLevel.Info);

    public void Warn(string title, string message) =>
        NotificationRequested?.Invoke(title, message, NotificationLevel.Warning);

    public void Error(string title, string message) =>
        NotificationRequested?.Invoke(title, message, NotificationLevel.Error);

    public void Success(string title, string message) =>
        NotificationRequested?.Invoke(title, message, NotificationLevel.Success);

    public void NoteDestroyedByTimer(Guid noteId, string title)
    {
        NoteTimerExpired?.Invoke(noteId, title);
        Warn("Заметка уничтожена", $"«{title}» надёжно удалена по таймеру");
    }
}

public enum NotificationLevel { Info, Warning, Error, Success }
