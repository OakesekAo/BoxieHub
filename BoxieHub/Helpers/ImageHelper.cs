using BoxieHub.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace BoxieHub.Helpers
{
    public static class ImageHelper
    {
        public static readonly string DefaultProfilePictureUrl = "/images/undraw_person.svg";

        public static async Task<FileUpload> GetImageUploadAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] fileData = ms.ToArray();

            if (ms.Length > 1 * 1024 * 1024)
            {
                throw new Exception("Image size is too large.");
            }

            FileUpload fileUpload = new ImageUpload
            {
                Id = Guid.NewGuid(),
                Data = fileData,
                ContentType = file.ContentType,
                FileName = file.FileName,
                FileSizeBytes = ms.Length,
                Created = DateTimeOffset.UtcNow
            };

            return fileUpload;
        }
        
        /// <summary>
        /// Get file upload from Blazor IBrowserFile (for audio, images, etc.)
        /// </summary>
        public static async Task<FileUpload> GetFileUploadAsync(IBrowserFile file, string category = "Unknown", long maxSizeBytes = 200 * 1024 * 1024)
        {
            using var ms = new MemoryStream();
            using var stream = file.OpenReadStream(maxAllowedSize: maxSizeBytes);
            await stream.CopyToAsync(ms);
            byte[] fileData = ms.ToArray();

            if (ms.Length > maxSizeBytes)
            {
                throw new Exception($"File size is too large. Maximum size is {maxSizeBytes / (1024 * 1024)} MB.");
            }

            FileUpload fileUpload = new FileUpload
            {
                Id = Guid.NewGuid(),
                Data = fileData,
                ContentType = file.ContentType,
                FileName = file.Name,
                FileCategory = category,
                FileSizeBytes = ms.Length,
                Created = DateTimeOffset.UtcNow
            };

            return fileUpload;
        }
    }
}
