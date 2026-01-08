using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Controllers
{
    [Route("uploads")]
    [ApiController]
    public class UploadsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<UploadsController> _logger;

        public UploadsController(
            ApplicationDbContext context,
            IFileStorageService fileStorageService,
            ILogger<UploadsController> logger)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [HttpGet("{id:guid}")]
        [OutputCache(VaryByRouteValueNames = ["id"], Duration = 60 * 60 * 24)]
        public async Task<IActionResult> GetImage(Guid id)
        {
            FileUpload? fileUpload = await _context.FileUploads.FirstOrDefaultAsync(i => i.Id == id);

            if (fileUpload == null)
            {
                _logger.LogWarning("File upload {FileId} not found", id);
                return NotFound();
            }

            // Check if file is stored in database (legacy)
            if (fileUpload.Data != null)
            {
                _logger.LogDebug("Serving file {FileId} from database", id);
                return File(fileUpload.Data, fileUpload.ContentType!);
            }

            // File is in external storage (S3, Dropbox, GDrive)
            if (string.IsNullOrEmpty(fileUpload.StoragePath))
            {
                _logger.LogError("File {FileId} has no data and no storage path", id);
                return NotFound("File data not available");
            }

            try
            {
                _logger.LogDebug("Downloading file {FileId} from {Provider}: {StoragePath}", 
                    id, fileUpload.Provider, fileUpload.StoragePath);

                // Download from external storage
                var stream = await _fileStorageService.DownloadFileAsync(
                    fileUpload.StoragePath,
                    fileUpload.UserStorageAccountId);

                _logger.LogDebug("Successfully downloaded file {FileId} from {Provider}", 
                    id, fileUpload.Provider);

                // Return as file stream
                return File(stream, fileUpload.ContentType!, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download file {FileId} from {Provider}: {StoragePath}", 
                    id, fileUpload.Provider, fileUpload.StoragePath);
                return StatusCode(500, "Failed to retrieve file from storage");
            }
        }
    }
}
