namespace ConSecOrg.Shared.DTOs.Projects;

public class UploadFileResponseDto
{
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Size { get; set; }
}
