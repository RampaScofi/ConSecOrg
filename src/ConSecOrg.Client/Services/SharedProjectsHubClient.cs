using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.SignalR.Client;

namespace ConSecOrg.Client.Services;

/// <summary>
/// SignalR client for real-time shared project + chat updates.
/// </summary>
public sealed class SharedProjectsHubClient : IAsyncDisposable
{
    private readonly SessionService _session;
    private readonly ModeService _mode;
    private HubConnection? _connection;
    private Guid? _subscribedProjectId;

    public SharedProjectsHubClient(SessionService session, ModeService mode)
    {
        _session = session;
        _mode = mode;
    }

    // Task events
    public event Action<SharedProjectTaskDto>? TaskCreated;
    public event Action<SharedProjectTaskDto>? TaskUpdated;
    public event Action<Guid>? TaskDeleted;
    public event Action<(Guid Id, Guid ColumnId, int Position)>? TaskMoved;

    // Column events
    public event Action<SharedProjectColumnDto>? ColumnCreated;
    public event Action<SharedProjectColumnDto>? ColumnUpdated;
    public event Action<Guid>? ColumnDeleted;
    public event Action<List<Guid>>? ColumnsReordered;

    // Chat events
    public event Action<ChatMessageDto>? ChatMessageReceived;
    // Fired when the remote user has read our messages in a direct chat
    // Payload is the chatKey from our perspective: "user:{theirId:N}"
    public event Action<string>? MessagesRead;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task EnsureConnectedAsync()
    {
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

            _connection.On<SharedProjectTaskDto>("SharedTaskCreated", t => TaskCreated?.Invoke(t));
            _connection.On<SharedProjectTaskDto>("SharedTaskUpdated", t => TaskUpdated?.Invoke(t));
            _connection.On<Guid>("SharedTaskDeleted", id => TaskDeleted?.Invoke(id));
            _connection.On<SharedTaskMovedSignalDto>("SharedTaskMoved", dto =>
                TaskMoved?.Invoke((dto.Id, dto.ColumnId, dto.Position)));

            _connection.On<SharedProjectColumnDto>("SharedColumnCreated", c => ColumnCreated?.Invoke(c));
            _connection.On<SharedProjectColumnDto>("SharedColumnUpdated", c => ColumnUpdated?.Invoke(c));
            _connection.On<Guid>("SharedColumnDeleted", id => ColumnDeleted?.Invoke(id));
            _connection.On<List<Guid>>("SharedColumnsReordered", ids => ColumnsReordered?.Invoke(ids));

            _connection.On<ChatMessageDto>("ChatMessageReceived", m => ChatMessageReceived?.Invoke(m));
            _connection.On<string>("MessagesRead", chatKey => MessagesRead?.Invoke(chatKey));

            _connection.Reconnected += async _ =>
            {
                if (_subscribedProjectId.HasValue && _connection is not null)
                    await _connection.InvokeAsync("JoinProject", _subscribedProjectId.Value);
            };
        }
        if (_connection.State == HubConnectionState.Disconnected)
            await _connection.StartAsync();
    }

    public async Task SubscribeAsync(Guid projectId)
    {
        await EnsureConnectedAsync();
        if (_subscribedProjectId == projectId) return;
        if (_subscribedProjectId.HasValue)
            await _connection!.InvokeAsync("LeaveProject", _subscribedProjectId.Value);
        await _connection!.InvokeAsync("JoinProject", projectId);
        _subscribedProjectId = projectId;
    }

    // Notify partner that we have read their direct messages
    public async Task NotifyDirectReadAsync(Guid partnerId)
    {
        await EnsureConnectedAsync();
        try { await _connection!.InvokeAsync("MarkDirectRead", partnerId); } catch { }
    }

    public async Task JoinGroupChatAsync(Guid groupChatId)
    {
        await EnsureConnectedAsync();
        try { await _connection!.InvokeAsync("JoinGroupChat", groupChatId); } catch { }
    }

    public async Task LeaveGroupChatAsync(Guid groupChatId)
    {
        if (_connection is null || !IsConnected) return;
        try { await _connection.InvokeAsync("LeaveGroupChat", groupChatId); } catch { }
    }

    public async Task UnsubscribeAsync()
    {
        if (_connection is null || !_subscribedProjectId.HasValue) return;
        try { await _connection.InvokeAsync("LeaveProject", _subscribedProjectId.Value); }
        catch { }
        _subscribedProjectId = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    // Internal DTO for the anonymous object sent by the server
    private class SharedTaskMovedSignalDto
    {
        public Guid Id { get; set; }
        public Guid ColumnId { get; set; }
        public int Position { get; set; }
    }
}
