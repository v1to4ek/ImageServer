using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageServer
{
    public class ImageStorage : IFileStorage<ImageInfo[]>
    {
        private readonly string _imagePath;
        private readonly string _thumbnailPath;

        public ImageStorage(string path)
        {
            _imagePath = Path.Combine(path, "images");
            _thumbnailPath = Path.Combine(path, "thumbnails");
            if (!Directory.Exists(_imagePath)) Directory.CreateDirectory(_imagePath);
            if (!Directory.Exists(_thumbnailPath)) Directory.CreateDirectory(_thumbnailPath);
        }

        public Task<ImageInfo[]> GetImagesUrls()
        {
            var imageUrls = Directory.GetFiles(_imagePath)
                .Select(file => "/images/" + Path.GetFileName(file));

            var thumbnailUrls = Directory.GetFiles(_thumbnailPath)
                .Select(file => "/thumbnails/" + Path.GetFileName(file));

            var result = imageUrls.Zip(thumbnailUrls, (img, thumb) => new ImageInfo
            {
                ImageUrl = img,
                ThumbnailUrl = thumb
            }).ToArray();

            return Task.FromResult(result);
        }

        public async Task DownloadImageAsync(IFormFile image)
        {
            var extention = Path.GetExtension(image.FileName).ToLower();
            var itemName = $"{Guid.NewGuid()}{extention}"; 

            var imagePath = Path.Combine(_imagePath, itemName);
            var thumbnailPath = Path.Combine(_thumbnailPath, itemName);

            using (var fileStream = new FileStream(imagePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            using var img = await Image.LoadAsync(image.OpenReadStream());

            img.Mutate(img => img.Resize(new ResizeOptions { Size = new Size(300, 0), Mode = ResizeMode.Max }));

            await img.SaveAsync(thumbnailPath);
        }
    }
}
