
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ImageServer.Services
{
    public class ImageProcessor : IImageProcessor
    {
        private static readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        public bool isValid(string imageName) => _allowedExtensions.Contains(Path.GetExtension(imageName).ToLower());

        public async Task<Stream> GenerateThumbnailAsync(Stream inputStream, int width, int height)
        {
            using var image = await Image.LoadAsync(inputStream);

            image.Mutate(image=>image.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            var outputStream = new MemoryStream();

            await image.SaveAsync(outputStream, new WebpEncoder());

            outputStream.Position = 0;

            return outputStream;
        }
    }
}
