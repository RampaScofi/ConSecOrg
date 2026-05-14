using ConSecOrg.Client.Infrastructure.Api;
using Microsoft.Extensions.Hosting;
using WpfApplication = System.Windows.Application;

namespace ConSecOrg.Client.Services;

public sealed class DestructionTimerService : BackgroundService
{
    private readonly INotesApiService _notes;
    private readonly SessionService _session;
    private readonly NotificationService _notifications;
    private readonly HashSet<Guid> _warned30 = [];
    private readonly HashSet<Guid> _warned5 = [];
    // Tracks notes already scheduled for deletion to prevent repeat notifications each tick.
    private readonly HashSet<Guid> _alreadyProcessed = [];

    public DestructionTimerService(INotesApiService notes, SessionService session, NotificationService notifications)
    {
        _notes = notes;
        _session = session;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Опрос каждые 5 секунд — чтобы короткие таймеры (30 сек / 1 мин) срабатывали вовремя
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { return; }

            if (!_session.IsAuthenticated) continue;

            try
            {
                var result = await _notes.GetNotesAsync(pageSize: 200);
                var nowUtc = DateTime.UtcNow;

                foreach (var note in result.Items.Where(n => n.ExpiresAt.HasValue))
                {
                    // Принудительно интерпретируем ExpiresAt как UTC (EF Core возвращает Unspecified)
                    var expiresUtc = note.ExpiresAt!.Value.Kind switch
                    {
                        DateTimeKind.Utc => note.ExpiresAt.Value,
                        DateTimeKind.Local => note.ExpiresAt.Value.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(note.ExpiresAt.Value, DateTimeKind.Utc)
                    };
                    var remaining = expiresUtc - nowUtc;

                    if (remaining <= TimeSpan.Zero)
                    {
                        if (_alreadyProcessed.Contains(note.Id)) continue;

                        // Mark processed and fire notification immediately — the note has
                        // expired regardless of whether the HTTP/DB delete succeeds right now.
                        // Corporate mode: NoteExpirationService on the server guarantees DB cleanup.
                        // Personal mode:  LocalNotesService.DeleteNoteAsync (EF) handles local DB.
                        _alreadyProcessed.Add(note.Id);
                        _warned30.Remove(note.Id);
                        _warned5.Remove(note.Id);
                        var noteId = note.Id;
                        var noteTitle = note.Title;
                        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
                            _notifications.NoteDestroyedByTimer(noteId, noteTitle));

                        // Best-effort delete (local mode must still do it; corporate fallback)
                        try { await _notes.DeleteNoteAsync(note.Id); } catch { }
                    }
                    else if (remaining <= TimeSpan.FromMinutes(5) && !_warned5.Contains(note.Id))
                    {
                        _warned5.Add(note.Id);
                        var msg = remaining.TotalSeconds < 60
                            ? $"«{note.Title}» будет уничтожена через {(int)remaining.TotalSeconds} сек"
                            : $"«{note.Title}» будет уничтожена через {(int)remaining.TotalMinutes + 1} мин";
                        WpfApplication.Current?.Dispatcher.Invoke(() =>
                            _notifications.Error("Таймер уничтожения", msg));
                    }
                    else if (remaining <= TimeSpan.FromMinutes(30) && !_warned30.Contains(note.Id))
                    {
                        _warned30.Add(note.Id);
                        WpfApplication.Current?.Dispatcher.Invoke(() =>
                            _notifications.Warn("Таймер уничтожения",
                                $"«{note.Title}» будет уничтожена через {(int)remaining.TotalMinutes} мин"));
                    }
                }
            }
            catch { /* фоновая служба — молча игнорируем */ }
        }
    }
}
