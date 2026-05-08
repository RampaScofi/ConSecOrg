using ConSecOrg.Shared.DTOs.Projects;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IChatApiService
{
    Task<IReadOnlyList<ChatSummaryDto>> GetChatsAsync();
    Task<IReadOnlyList<ChatMessageDto>> GetProjectMessagesAsync(Guid projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetDirectMessagesAsync(Guid otherUserId);
    Task<ChatMessageDto> SendMessageAsync(SendChatMessageDto request);
    Task<(string Url, string FileName)> UploadChatFileAsync(string localFilePath);
}
