using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace ImageServer.Services
{

    /// <summary>
    /// Стратегия конвертирования входного изображения в формат webp.
    /// Создаёт новый Stream, готовый к записи, на выходе.
    /// Предполагает использовние Stream в качестве TResult и TInput внутри метода ProcessAsync интерфейса IImageProcessor.
    /// </summary>
    public class ImageConversionProcessor : IProcessingStrategy<Stream, Stream>
    {
        private readonly int _qualityLevel;

        public ImageConversionProcessor(int qualityLevel = 75)
        {
            _qualityLevel = qualityLevel;
        }

        public async Task<Stream> ProcessAsync(Stream input, CancellationToken ct = default)
        {
            using var image = await Image.LoadAsync(input,ct);  

            var outputStream = new MemoryStream();

            await image.SaveAsync(outputStream, new WebpEncoder { Quality = _qualityLevel }, ct);

            outputStream.Position = 0;
            
            return outputStream;
        }
    }
}
