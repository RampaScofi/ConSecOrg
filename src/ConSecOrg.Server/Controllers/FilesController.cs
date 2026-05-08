using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
         ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt",
         ".zip", ".rar", ".7z", ".mp4", ".mp3"];

    private const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UploadFileResponseDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");
        if (file.Length > MaxFileSize)
            return BadRequest("File exceeds 20 MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest($"File type '{ext}' is not allowed.");

        var userId = CurrentUserId;
        var uploadsDir = Path.Combine("wwwroot", "uploads", userId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, safeFileName);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream, ct);

        var url = $"/uploads/{userId}/{safeFileName}";
        return Ok(new UploadFileResponseDto
        {
            Url = url,
            FileName = file.FileName,
            FileSize = file.Length,
            ContentType = file.ContentType
        });
    }
}
