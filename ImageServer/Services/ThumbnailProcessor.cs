using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ImageServer.Services
{
    public class ThumbnailProcessor : IProcessingStrategy<Stream>
    {
        private readonly int _width;

        private readonly int _height;

        public ThumbnailProcessor(int width, int height)
        { 
            _width = width;
            _height = height;
        }

        public async Task<Stream> ProcessAsync(Stream inputStream, CancellationToken ct = default)
        {
            using var image = await Image.LoadAsync(inputStream, ct);

            image.Mutate(image => image.Resize(new ResizeOptions
            {
                Size = new Size(_width, _height),
                Mode = ResizeMode.Max
            }));

            var outputStream = new MemoryStream();

            await image.SaveAsync(outputStream, new WebpEncoder(), ct);

            outputStream.Position = 0;

            return outputStream;
        }
    }
}
