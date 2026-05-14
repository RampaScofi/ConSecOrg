using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.DTOs.Contacts;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.DTOs.Projects;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.DTOs.Dashboard;
using ConSecOrg.Shared.DTOs.Users;
using ConSecOrg.Shared.Pagination;
using RestSharp;
using System.Net.Http;
using System.Text.Json;

namespace ConSecOrg.Client.Infrastructure.Api;

public sealed class ApiClient :
    IAuthApiService,
    INotesApiService,
    ITasksApiService,
    IContactsApiService,
    IAuditApiService,
    IDashboardApiService,
    IUserSearchApiService,
    ISharedProjectsApiService,
    IChatApiService,
    IUsersManagementApiService
{
    private readonly SessionService _session;
    private readonly ModeService _modeService;
    private string? _currentServerUrl;
    private RestClient? _cachedClient;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(SessionService session, ModeService modeService)
    {
        _session = session;
        _modeService = modeService;
    }

    private RestClient GetClient()
    {
        if (_cachedClient is null || _currentServerUrl != _modeService.ServerUrl)
        {
            _currentServerUrl = _modeService.ServerUrl;
            _cachedClient = new RestClient(_currentServerUrl);
        }
        return _cachedClient;
    }

    private RestRequest Req(string path, Method method = Method.Get)
    {
        var req = new RestRequest(path, method);
        if (_session.AccessToken is not null)
            req.AddHeader("Authorization", $"Bearer {_session.AccessToken}");
        return req;
    }

    private async Task<T> SendAsync<T>(RestRequest req)
    {
        RestResponse response;
        try
        {
            response = await GetClient().ExecuteAsync(req);
        }
        catch (Exception)
        {
            throw new HttpRequestException("Нет соединения с сервером. Проверьте подключение к сети.");
        }

        if (!response.IsSuccessful)
            throw new HttpRequestException(BuildRussianError(response));
        return JsonSerializer.Deserialize<T>(response.Content!, _json)!;
    }

    private static string BuildRussianError(RestResponse response)
    {
        // Try to extract server-provided message from JSON body
        string? serverMsg = null;
        if (!string.IsNullOrWhiteSpace(response.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var msgProp))
                    serverMsg = msgProp.GetString();
                // Validation errors: collect field messages
                if (root.TryGetProperty("errors", out var errProp))
                {
                    var parts = new List<string>();
                    foreach (var field in errProp.EnumerateObject())
                        foreach (var msg in field.Value.EnumerateArray())
                            parts.Add(msg.GetString() ?? string.Empty);
                    if (parts.Count > 0)
                        return string.Join(" ", parts);
                }
            }
            catch { /* not JSON */ }
        }

        return (int?)response.StatusCode switch
        {
            400 => serverMsg ?? "Некорректный запрос. Проверьте введённые данные.",
            401 => TranslateAuthError(serverMsg),
            403 => TranslateForbiddenError(serverMsg),
            404 => "Запрошенные данные не найдены.",
            405 => "Эта операция не поддерживается.",
            409 => serverMsg?.Contains("already sent", StringComparison.OrdinalIgnoreCase) == true
                    ? "Запрос уже отправлен."
                    : serverMsg?.Contains("already", StringComparison.OrdinalIgnoreCase) == true
                    ? "Запись уже существует."
                    : "Конфликт данных. Попробуйте ещё раз.",
            422 => "Ошибка целостности данных. Возможно, данные были изменены.",
            423 => "Аккаунт заблокирован. Обратитесь к администратору.",
            429 => "Слишком много запросов. Подождите немного и попробуйте снова.",
            500 => "Ошибка на сервере. Попробуйте позже.",
            502 or 503 or 504 => "Сервер недоступен. Попробуйте позже.",
            null => "Нет соединения с сервером. Проверьте подключение к сети.",
            _ => serverMsg ?? $"Ошибка сервера (код {(int?)response.StatusCode}). Попробуйте позже."
        };
    }

    private static string TranslateAuthError(string? serverMsg)
    {
        if (serverMsg is null) return "Неверный логин или пароль.";
        var s = serverMsg.ToLowerInvariant();
        if (s.Contains("lock")) return "Аккаунт заблокирован. Обратитесь к администратору.";
        if (s.Contains("invalid") || s.Contains("incorrect") || s.Contains("wrong"))
            return "Неверный логин или пароль. Попробуйте ещё раз.";
        if (s.Contains("not found") || s.Contains("no user"))
            return "Пользователь с таким логином не найден.";
        if (s.Contains("expired") || s.Contains("token"))
            return "Сессия истекла. Пожалуйста, войдите снова.";
        return "Неверный логин или пароль.";
    }

    private static string TranslateForbiddenError(string? serverMsg)
    {
        if (serverMsg is null) return "Недостаточно прав для выполнения этого действия.";
        var s = serverMsg.ToLowerInvariant();
        if (s.Contains("device")) return "Устройство не распознано. Доступ запрещён.";
        if (s.Contains("role") || s.Contains("permission") || s.Contains("access"))
            return "Недостаточно прав для выполнения этого действия.";
        return "Доступ запрещён.";
    }

    // ── Auth ──────────────────────────────────────────────────────────────────
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var req = Req("api/v1/auth/login", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<LoginResponseDto>(req);
    }

    public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
    {
        var req = Req("api/v1/auth/refresh", Method.Post);
        req.AddJsonBody(new RefreshRequestDto { RefreshToken = refreshToken });
        return await SendAsync<LoginResponseDto>(req);
    }

    public async Task LogoutAsync()
    {
        var req = Req("api/v1/auth/logout", Method.Post);
        await GetClient().ExecuteAsync(req);
    }

    public Task<UserInfoDto> GetMeAsync() => SendAsync<UserInfoDto>(Req("api/v1/auth/me"));

    public async Task RegisterAsync(RegisterRequestDto request)
    {
        var req = Req("api/v1/auth/bootstrap", Method.Post);
        req.AddJsonBody(request);
        var response = await GetClient().ExecuteAsync(req);
        if (!response.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)response.StatusCode}: {response.Content}");
    }

    public async Task RegisterSelfAsync(RegisterRequestDto request)
    {
        var req = Req("api/v1/auth/register-self", Method.Post);
        req.AddJsonBody(request);
        var response = await GetClient().ExecuteAsync(req);
        if (!response.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)response.StatusCode}: {response.Content}");
    }

    public async Task ChangePasswordAsync(ChangePasswordRequestDto request)
    {
        var req = Req("api/v1/auth/change-password", Method.Post);
        req.AddJsonBody(request);
        var response = await GetClient().ExecuteAsync(req);
        if (!response.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)response.StatusCode}: {response.Content}");
    }

    public async Task ChangeEmailAsync(ChangeEmailRequestDto request)
    {
        var req = Req("api/v1/auth/change-email", Method.Post);
        req.AddJsonBody(request);
        var response = await GetClient().ExecuteAsync(req);
        if (!response.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)response.StatusCode}: {response.Content}");
    }

    // ── Notes ─────────────────────────────────────────────────────────────────
    public Task<PagedResponse<NoteDto>> GetNotesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var req = Req($"api/v1/notes?page={page}&pageSize={pageSize}{(search is not null ? $"&search={Uri.EscapeDataString(search)}" : "")}");
        return SendAsync<PagedResponse<NoteDto>>(req);
    }

    public Task<NoteDto> GetNoteAsync(Guid id) => SendAsync<NoteDto>(Req($"api/v1/notes/{id}"));

    public Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string query) =>
        SendAsync<IReadOnlyList<NoteDto>>(Req($"api/v1/notes/search?q={Uri.EscapeDataString(query)}"));

    public async Task<Guid> CreateNoteAsync(CreateNoteRequestDto request)
    {
        var req = Req("api/v1/notes", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<Guid>(req);
    }

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequestDto request)
    {
        var req = Req($"api/v1/notes/{id}", Method.Put);
        req.AddJsonBody(request);
        await GetClient().ExecuteAsync(req);
    }

    public async Task DeleteNoteAsync(Guid id)
    {
        var resp = await GetClient().ExecuteAsync(Req($"api/v1/notes/{id}", Method.Delete));
        // 404 = already gone — treat as success so the timer removes it from memory
        if (!resp.IsSuccessful && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new HttpRequestException(
                $"DELETE /notes/{id} returned {(int)resp.StatusCode}: {resp.ErrorMessage ?? resp.StatusDescription}");
    }

    public async Task SetDestructionTimerAsync(Guid id, DateTime expiresAt)
    {
        var req = Req($"api/v1/notes/{id}/timer", Method.Post);
        req.AddJsonBody(new SetTimerRequestDto { ExpiresAt = expiresAt });
        await GetClient().ExecuteAsync(req);
    }

    // ── Tasks ─────────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<TaskItemDto>> GetBoardAsync() => SendAsync<IReadOnlyList<TaskItemDto>>(Req("api/v1/tasks"));
    public Task<IReadOnlyList<TaskItemDto>> GetDueAsync(int days = 7) => SendAsync<IReadOnlyList<TaskItemDto>>(Req($"api/v1/tasks/due?days={days}"));

    public async Task<Guid> CreateTaskAsync(CreateTaskRequestDto request)
    {
        var req = Req("api/v1/tasks", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<Guid>(req);
    }

    public async Task UpdateTaskAsync(Guid id, UpdateTaskRequestDto request)
    {
        var req = Req($"api/v1/tasks/{id}", Method.Put);
        req.AddJsonBody(request);
        await GetClient().ExecuteAsync(req);
    }

    public async Task MoveTaskAsync(Guid id, MoveTaskRequestDto request)
    {
        var req = Req($"api/v1/tasks/{id}/move", Method.Patch);
        req.AddJsonBody(request);
        await GetClient().ExecuteAsync(req);
    }

    public async Task DeleteTaskAsync(Guid id) => await GetClient().ExecuteAsync(Req($"api/v1/tasks/{id}", Method.Delete));

    // ── Contacts ──────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<ContactDto>> GetContactsAsync() => SendAsync<IReadOnlyList<ContactDto>>(Req("api/v1/contacts"));
    public Task<ContactDto> GetContactAsync(Guid id) => SendAsync<ContactDto>(Req($"api/v1/contacts/{id}"));

    public async Task<Guid> CreateContactAsync(CreateContactRequestDto request)
    {
        var req = Req("api/v1/contacts", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<Guid>(req);
    }

    public async Task UpdateContactAsync(Guid id, UpdateContactRequestDto request)
    {
        var req = Req($"api/v1/contacts/{id}", Method.Put);
        req.AddJsonBody(request);
        await GetClient().ExecuteAsync(req);
    }

    public async Task DeleteContactAsync(Guid id) => await GetClient().ExecuteAsync(Req($"api/v1/contacts/{id}", Method.Delete));

    // ── Audit ─────────────────────────────────────────────────────────────────
    public Task<PagedResponse<AuditLogDto>> GetLogsAsync(int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null, string? action = null)
    {
        var url = $"api/v1/audit?page={page}&pageSize={pageSize}";
        if (from.HasValue) url += $"&from={from.Value:O}";
        if (to.HasValue) url += $"&to={to.Value:O}";
        if (!string.IsNullOrEmpty(action)) url += $"&action={Uri.EscapeDataString(action)}";
        return SendAsync<PagedResponse<AuditLogDto>>(Req(url));
    }

    public Task<AuditChainVerifyResultDto> VerifyChainAsync() => SendAsync<AuditChainVerifyResultDto>(Req("api/v1/audit/verify"));

    public async Task<byte[]> ExportCsvAsync()
    {
        var response = await GetClient().ExecuteAsync(Req("api/v1/audit/export"));
        if (!response.IsSuccessful)
            throw new HttpRequestException($"Export error {(int?)response.StatusCode}: {response.Content}");
        return response.RawBytes ?? [];
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public Task<SecurityDashboardDto> GetSecurityDashboardAsync() => SendAsync<SecurityDashboardDto>(Req("api/v1/dashboard/security"));

    // ── User Search & Contact Requests ─────────────────────────────────────────
    public Task<IReadOnlyList<UserSearchDto>> SearchUsersAsync(string query) =>
        SendAsync<IReadOnlyList<UserSearchDto>>(Req($"api/v1/users/search?q={Uri.EscapeDataString(query)}"));

    public async Task SendContactRequestAsync(SendContactRequestDto request)
    {
        var req = Req("api/v1/contacts/requests", Method.Post);
        req.AddJsonBody(request);
        var response = await GetClient().ExecuteAsync(req);
        if (!response.IsSuccessful && response.StatusCode != System.Net.HttpStatusCode.Conflict)
            throw new HttpRequestException($"API error {(int?)response.StatusCode}: {response.Content}");
    }

    public Task<IReadOnlyList<ContactRequestDto>> GetPendingRequestsAsync() =>
        SendAsync<IReadOnlyList<ContactRequestDto>>(Req("api/v1/contacts/requests/pending"));

    public Task<IReadOnlyList<ContactRequestDto>> GetSentRequestsAsync() =>
        SendAsync<IReadOnlyList<ContactRequestDto>>(Req("api/v1/contacts/requests/sent"));

    public async Task AcceptContactRequestAsync(Guid requestId)
    {
        var req = Req($"api/v1/contacts/requests/{requestId}/accept", Method.Post);
        await GetClient().ExecuteAsync(req);
    }

    public async Task DeclineContactRequestAsync(Guid requestId)
    {
        var req = Req($"api/v1/contacts/requests/{requestId}/decline", Method.Post);
        await GetClient().ExecuteAsync(req);
    }

    // ── Shared Projects ────────────────────────────────────────────────────────
    public Task<IReadOnlyList<SharedProjectDto>> GetMyProjectsAsync() =>
        SendAsync<IReadOnlyList<SharedProjectDto>>(Req("api/v1/projects"));

    public async Task<SharedProjectDto> CreateProjectAsync(CreateSharedProjectDto request)
    {
        var req = Req("api/v1/projects", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectDto>(req);
    }

    public async Task<SharedProjectDto> JoinProjectAsync(JoinSharedProjectDto request)
    {
        var req = Req("api/v1/projects/join", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectDto>(req);
    }

    public async Task DeleteProjectAsync(Guid id) =>
        await GetClient().ExecuteAsync(Req($"api/v1/projects/{id}", Method.Delete));

    public async Task LeaveProjectAsync(Guid id) =>
        await GetClient().ExecuteAsync(Req($"api/v1/projects/{id}/leave", Method.Post));

    public Task<IReadOnlyList<SharedProjectTaskDto>> GetProjectTasksAsync(Guid projectId) =>
        SendAsync<IReadOnlyList<SharedProjectTaskDto>>(Req($"api/v1/projects/{projectId}/tasks"));

    public async Task<SharedProjectTaskDto> CreateProjectTaskAsync(Guid projectId, CreateSharedProjectTaskDto request)
    {
        var req = Req($"api/v1/projects/{projectId}/tasks", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectTaskDto>(req);
    }

    public async Task<SharedProjectTaskDto> UpdateProjectTaskAsync(Guid projectId, Guid taskId, UpdateSharedProjectTaskDto request)
    {
        var req = Req($"api/v1/projects/{projectId}/tasks/{taskId}", Method.Put);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectTaskDto>(req);
    }

    public async Task DeleteProjectTaskAsync(Guid projectId, Guid taskId) =>
        await GetClient().ExecuteAsync(Req($"api/v1/projects/{projectId}/tasks/{taskId}", Method.Delete));

    public async Task MoveProjectTaskAsync(Guid projectId, Guid taskId, MoveSharedTaskDto request)
    {
        var req = Req($"api/v1/projects/{projectId}/tasks/{taskId}/move", Method.Patch);
        req.AddJsonBody(request);
        await GetClient().ExecuteAsync(req);
    }

    // ── Shared Project Columns ────────────────────────────────────────────────
    public Task<IReadOnlyList<SharedProjectColumnDto>> GetColumnsAsync(Guid projectId) =>
        SendAsync<IReadOnlyList<SharedProjectColumnDto>>(Req($"api/v1/projects/{projectId}/columns"));

    public async Task<SharedProjectColumnDto> CreateColumnAsync(Guid projectId, CreateSharedProjectColumnDto request)
    {
        var req = Req($"api/v1/projects/{projectId}/columns", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectColumnDto>(req);
    }

    public async Task<SharedProjectColumnDto> UpdateColumnAsync(Guid projectId, Guid columnId, UpdateSharedProjectColumnDto request)
    {
        var req = Req($"api/v1/projects/{projectId}/columns/{columnId}", Method.Put);
        req.AddJsonBody(request);
        return await SendAsync<SharedProjectColumnDto>(req);
    }

    public async Task DeleteColumnAsync(Guid projectId, Guid columnId) =>
        await GetClient().ExecuteAsync(Req($"api/v1/projects/{projectId}/columns/{columnId}", Method.Delete));

    public async Task ReorderColumnsAsync(Guid projectId, List<Guid> orderedIds)
    {
        var req = Req($"api/v1/projects/{projectId}/columns/reorder", Method.Patch);
        req.AddJsonBody(orderedIds);
        await GetClient().ExecuteAsync(req);
    }

    // ── Chat ──────────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<ChatSummaryDto>> GetChatsAsync() =>
        SendAsync<IReadOnlyList<ChatSummaryDto>>(Req("api/v1/chats"));

    public Task<IReadOnlyList<ChatMessageDto>> GetProjectMessagesAsync(Guid projectId) =>
        SendAsync<IReadOnlyList<ChatMessageDto>>(Req($"api/v1/chats/project/{projectId}"));

    public Task<IReadOnlyList<ChatMessageDto>> GetDirectMessagesAsync(Guid otherUserId) =>
        SendAsync<IReadOnlyList<ChatMessageDto>>(Req($"api/v1/chats/direct/{otherUserId}"));

    public Task<IReadOnlyList<ChatMessageDto>> GetGroupMessagesAsync(Guid groupChatId) =>
        SendAsync<IReadOnlyList<ChatMessageDto>>(Req($"api/v1/groupchats/{groupChatId}/messages"));

    public async Task MarkReadAsync(string chatKey)
    {
        var req = Req("api/v1/chats/mark-read", Method.Post);
        req.AddJsonBody(new { chatKey });
        await GetClient().ExecuteAsync(req);
    }

    public async Task<ChatMessageDto> SendMessageAsync(SendChatMessageDto request)
    {
        var req = Req("api/v1/chats", Method.Post);
        req.AddJsonBody(request);
        return await SendAsync<ChatMessageDto>(req);
    }

    public async Task<(string FileId, string FileName, string Url, long Size)> UploadChatFileAsync(string localFilePath)
    {
        var req = Req("api/v1/files/upload", Method.Post);
        req.AddFile("file", localFilePath);
        var resp = await GetClient().ExecuteAsync<UploadFileResponseDto>(req);
        if (!resp.IsSuccessful || resp.Data is null)
            throw new HttpRequestException($"Ошибка загрузки файла: {resp.Content}");
        return (resp.Data.FileId, resp.Data.FileName, resp.Data.Url, resp.Data.Size);
    }

    public async Task DownloadFileAsync(string fileId, string savePath)
    {
        var req = Req($"api/v1/files/{fileId}");
        var resp = await GetClient().DownloadDataAsync(req);
        if (resp is null || resp.Length == 0)
            throw new HttpRequestException("Файл не найден на сервере.");
        await System.IO.File.WriteAllBytesAsync(savePath, resp);
    }

    // ── Group Chats ───────────────────────────────────────────────────────────
    public Task<List<GroupChatDto>> GetGroupChatsAsync() =>
        SendAsync<List<GroupChatDto>>(Req("api/v1/groupchats"));

    public async Task<GroupChatDto> CreateGroupChatAsync(CreateGroupChatDto dto)
    {
        var req = Req("api/v1/groupchats", Method.Post);
        req.AddJsonBody(dto);
        return await SendAsync<GroupChatDto>(req);
    }

    public async Task AddGroupChatMemberAsync(Guid groupChatId, Guid userId)
    {
        var req = Req($"api/v1/groupchats/{groupChatId}/members", Method.Post);
        req.AddJsonBody(new { userId });
        var resp = await GetClient().ExecuteAsync(req);
        if (!resp.IsSuccessful)
            throw new HttpRequestException(BuildRussianError(resp));
    }

    public async Task RemoveGroupChatMemberAsync(Guid groupChatId, Guid userId)
    {
        var resp = await GetClient().ExecuteAsync(
            Req($"api/v1/groupchats/{groupChatId}/members/{userId}", Method.Delete));
        if (!resp.IsSuccessful)
            throw new HttpRequestException(BuildRussianError(resp));
    }

    public async Task DeleteGroupChatAsync(Guid groupChatId)
    {
        var resp = await GetClient().ExecuteAsync(
            Req($"api/v1/groupchats/{groupChatId}", Method.Delete));
        if (!resp.IsSuccessful)
            throw new HttpRequestException(BuildRussianError(resp));
    }

    // ── Users Management ──────────────────────────────────────────────────────
    public Task<IReadOnlyList<UserDto>> GetAllUsersAsync() =>
        SendAsync<IReadOnlyList<UserDto>>(Req("api/v1/users"));

    public async Task AssignRoleAsync(Guid userId, Guid roleId)
    {
        var req = Req($"api/v1/users/{userId}/role", Method.Post);
        req.AddJsonBody(new AssignRoleRequestDto { RoleId = roleId });
        var resp = await GetClient().ExecuteAsync(req);
        if (!resp.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)resp.StatusCode}: {resp.Content}");
    }

    public async Task LockUserAsync(Guid userId)
    {
        var resp = await GetClient().ExecuteAsync(Req($"api/v1/users/{userId}/lock", Method.Post));
        if (!resp.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)resp.StatusCode}: {resp.Content}");
    }

    public async Task UnlockUserAsync(Guid userId)
    {
        var resp = await GetClient().ExecuteAsync(Req($"api/v1/users/{userId}/unlock", Method.Post));
        if (!resp.IsSuccessful)
            throw new HttpRequestException($"API error {(int?)resp.StatusCode}: {resp.Content}");
    }
}
