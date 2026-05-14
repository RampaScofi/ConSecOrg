using Microsoft.AspNetCore.SignalR.Client;

namespace ConSecOrg.Client.Services;

/// <summary>
/// SignalR client for real-time regular Kanban board sync (corporate mode).
/// </summary>
public sealed class BoardHubClient : IAsyncDisposable
{
    private readonly SessionService _session;
    private readonly ModeService _mode;
    private HubConnection? _connection;

    public BoardHubClient(SessionService session, ModeService mode)
    {
        _session = session;
        _mode = mode;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>Fired when another client moves a task.</summary>
    public event Action<Guid, int, int>? TaskMoved;

    public async Task EnsureConnectedAsync()
    {
        if (!_mode.IsCorporate) return;
        if (IsConnected) return;

        if (_connection is null)
        {
            var url = _mode.ServerUrl?.TrimEnd('/') + "/hubs/board";
            _connection = new HubConnectionBuilder()
                .WithUrl(url, opts =>
                {
                    opts.AccessTokenProvider = () => Task.FromResult(_session.AccessToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<Guid, int, int>("TaskMoved", (id, col, status) =>
                TaskMoved?.Invoke(id, col, status));

            _connection.Reconnected += async _ =>
            {
                if (_connection is not null)
                    await _connection.InvokeAsync("JoinBoard");
            };
        }

        if (_connection.State == HubConnectionState.Disconnected)
            await _connection.StartAsync();

        await _connection.InvokeAsync("JoinBoard");
    }

    public async Task JoinGroupChatAsync(Guid groupChatId)
    {
        if (!IsConnected) return;
        try { await _connection!.InvokeAsync("JoinGroupChat", groupChatId); } catch { }
    }

    public async Task LeaveGroupChatAsync(Guid groupChatId)
    {
        if (!IsConnected) return;
        try { await _connection!.InvokeAsync("LeaveGroupChat", groupChatId); } catch { }
    }

    public async Task NotifyTaskMovedAsync(Guid taskId, int newColumn, int newStatus)
    {
        if (!IsConnected) return;
        try { await _connection!.InvokeAsync("TaskMoved", taskId, newColumn, newStatus); }
        catch { }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        try
        {
            if (IsConnected) await _connection.InvokeAsync("LeaveBoard");
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
