using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ImageServer.Services
{
    /// <summary>
    /// Стратегия генерации превью(сжатой версии) входного изображения.
    /// Создаёт новый Stream, готовый к записи, на выходе.
    /// Предполагает использовние Stream в качестве TResult и TInput внутри метода ProcessAsync интерфейса IImageProcessor.
    /// </summary>
    public class PreviewConversionProcessor : IProcessingStrategy<Stream, Stream>
    {
        private readonly int _width;

        private readonly int _height;

        public PreviewConversionProcessor(int width, int height)
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
