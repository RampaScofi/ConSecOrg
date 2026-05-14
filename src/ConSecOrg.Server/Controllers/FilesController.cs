using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController(IWebHostEnvironment env) : ControllerBase
{
    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
         ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt",
         ".zip", ".rar", ".7z", ".mp4", ".mp3"];

    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

    private string UploadsRoot => Path.Combine(env.ContentRootPath, "uploads");

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<UploadFileResponseDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Файл не предоставлен." });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "Размер файла превышает 50 МБ." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = $"Тип файла '{ext}' не разрешён." });

        var fileId = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(UploadsRoot, fileId);
        Directory.CreateDirectory(dir);

        // Sanitize original filename, keep for download
        var safeOrigName = string.Concat(Path.GetFileName(file.FileName).Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeOrigName)) safeOrigName = $"file{ext}";

        var filePath = Path.Combine(dir, safeOrigName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream, ct);

        return Ok(new UploadFileResponseDto
        {
            FileId = fileId,
            FileName = safeOrigName,
            Url = $"/api/v1/files/{fileId}",
            Size = file.Length
        });
    }

    [HttpGet("{fileId}")]
    public IActionResult Download(string fileId)
    {
        // Must be 32 hex characters (GUID without dashes)
        if (!Regex.IsMatch(fileId, @"^[a-f0-9]{32}$"))
            return BadRequest();

        var dir = Path.Combine(UploadsRoot, fileId);
        if (!Directory.Exists(dir)) return NotFound();

        var files = Directory.GetFiles(dir);
        if (files.Length == 0) return NotFound();

        var filePath = files[0];
        var fileName = Path.GetFileName(filePath);
        return PhysicalFile(filePath, GetMimeType(fileName), fileName);
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain; charset=utf-8",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
}
