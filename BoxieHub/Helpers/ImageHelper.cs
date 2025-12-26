using BoxieHub.Models;

namespace BoxieHub.Helpers
{
    public static class ImageHelper
    {
        public static readonly string DefaultProfilePictureUrl = "/images/undraw_person.svg";

        public static async Task<ImageUpload> GetImageUploadAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] imageData = ms.ToArray();

            if (ms.Length > 1 * 1024 * 1024)
            {
                throw new Exception("Image size is to large.");
            }

            ImageUpload imageUpload = new()
            {
                Id = Guid.NewGuid(),
                Data = imageData,
                Type = file.ContentType
            };

            return imageUpload;
        }
    }
}
