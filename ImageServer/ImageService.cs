namespace ImageServer
{
    //пока-что просто посредник
    public class ImageService
    {
        private readonly IStorage _storage;

        private readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        public ImageService(IStorage storage)
        {
            _storage = storage;
        }

        public async Task UploadImageAsync(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var form = await request.ReadFormAsync();
            var images = form.Files;

            foreach( var image in images )
            {
                var extention = Path.GetExtension(image.FileName).ToLower();
                if(!_allowedExtensions.Contains(extention)) { continue; }
                await _storage.Upload(image);
            }

        }

        public async Task<string[]> DownloadImageAsync(HttpContext context)
        {
            return await _storage.Download();
        }
    }
}
