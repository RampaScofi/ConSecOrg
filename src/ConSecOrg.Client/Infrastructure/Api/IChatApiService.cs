using ConSecOrg.Shared.DTOs.Projects;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IChatApiService
{
    Task<IReadOnlyList<ChatSummaryDto>> GetChatsAsync();
    Task<IReadOnlyList<ChatMessageDto>> GetProjectMessagesAsync(Guid projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetDirectMessagesAsync(Guid otherUserId);
    Task<IReadOnlyList<ChatMessageDto>> GetGroupMessagesAsync(Guid groupChatId);
    Task<ChatMessageDto> SendMessageAsync(SendChatMessageDto request);
    Task<(string FileId, string FileName, string Url, long Size)> UploadChatFileAsync(string localFilePath);
    Task DownloadFileAsync(string fileId, string savePath);

    Task MarkReadAsync(string chatKey);
    Task DeleteChatMessageAsync(Guid messageId);

    // Group chats
    Task<List<GroupChatDto>> GetGroupChatsAsync();
    Task<GroupChatDto> GetGroupChatAsync(Guid id);
    Task<GroupChatDto> CreateGroupChatAsync(CreateGroupChatDto dto);
    Task RenameGroupChatAsync(Guid id, string newName);
    Task AddGroupChatMemberAsync(Guid groupChatId, Guid userId);
    Task RemoveGroupChatMemberAsync(Guid groupChatId, Guid userId);
    Task TransferGroupOwnershipAsync(Guid groupChatId, Guid newOwnerId);
    Task DeleteGroupChatAsync(Guid groupChatId);
}
