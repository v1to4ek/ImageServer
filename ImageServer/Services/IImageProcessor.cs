namespace ImageServer.Services
{
    public interface IImageProcessor
    {
        public Task<Stream> GenerateThumbnailAsync(Stream inputStream, int width, int height);
    }
}
