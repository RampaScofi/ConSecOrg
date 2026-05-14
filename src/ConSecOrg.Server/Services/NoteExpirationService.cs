using ConSecOrg.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Server.Services;

/// <summary>
/// Server-side background service that guarantees expired notes are physically deleted from the
/// database. Runs every 30 seconds independently of the client timer. This is the authoritative
/// cleanup mechanism — the client's DestructionTimerService is responsible only for UI
/// notifications and local-mode deletion.
/// </summary>
public sealed class NoteExpirationService(
    IServiceProvider services,
    ILogger<NoteExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit after startup so migrations can complete first
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredNotesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NoteExpirationService: unexpected error");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { return; }
        }
    }

    private async Task DeleteExpiredNotesAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Secure overwrite of encrypted fields before deletion (best-effort)
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE notes " +
                "SET content_encrypted = CRYPT_GEN_RANDOM(CAST(DATALENGTH(content_encrypted) AS INT)), " +
                "    content_nonce      = CRYPT_GEN_RANDOM(16), " +
                "    content_hmac       = CRYPT_GEN_RANDOM(32) " +
                "WHERE ExpiresAt IS NOT NULL AND ExpiresAt <= GETUTCDATE()", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NoteExpirationService: secure overwrite failed, proceeding with delete");
        }

        // Hard delete — this MUST succeed regardless of audit or other side-effects
        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM notes WHERE ExpiresAt IS NOT NULL AND ExpiresAt <= GETUTCDATE()", ct);

        if (deleted > 0)
            logger.LogInformation("NoteExpirationService: deleted {Count} expired note(s)", deleted);
    }
}
