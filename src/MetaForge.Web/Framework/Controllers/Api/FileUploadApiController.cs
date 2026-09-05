namespace MetaForge.Web.Controllers.Api;

/// <summary>
/// Handles dynamic form file uploads for <c>FileUpload</c> control fields.
/// Stored files are written under <c>wwwroot/uploads/{yyyy}/{MM}</c> and served as static content.
/// </summary>
[Authorize]
[ApiController]
[Route("api/metaforge/files")]
public class FileUploadApiController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadApiController> _logger;

    public FileUploadApiController(IWebHostEnvironment environment, ILogger<FileUploadApiController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(AppConstants.MaxUploadFileSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file was provided." });

        if (file.Length > AppConstants.MaxUploadFileSizeBytes)
        {
            var maxMb = AppConstants.MaxUploadFileSizeBytes / (1024 * 1024);
            return BadRequest(new { error = $"File exceeds the maximum allowed size of {maxMb} MB." });
        }

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName);

        if (AppConstants.BlockedUploadExtensions.Contains(extension))
            return BadRequest(new { error = $"Files of type '{extension}' are not allowed." });

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var now = DateTime.UtcNow;
        var relativeFolder = Path.Combine(AppConstants.UploadsFolderName, now.ToString("yyyy"), now.ToString("MM"));
        var absoluteFolder = Path.Combine(webRoot, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, storedName);

        await using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = "/" + Path.Combine(relativeFolder, storedName).Replace('\\', '/');
        var isImage = AppConstants.ImageFileExtensions.Contains(extension);

        _logger.LogInformation("File uploaded: {OriginalName} -> {Url} ({Size} bytes)", originalName, url, file.Length);

        return Ok(new
        {
            url,
            fileName = originalName,
            size = file.Length,
            contentType = file.ContentType,
            isImage
        });
    }
}
