namespace ImageServer
{
    public class ImageService
    {
        private readonly IStorage _storage;

        public ImageService(IStorage storage)
        {
            _storage = storage;
        }

        public async Task UploadImageAsync(HttpContext context)
        {

        }

        public async Task DownloadImageAsync(HttpContext context)
        {

        }
    }
}
