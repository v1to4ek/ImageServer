namespace ImageServer
{
    //пока-что просто посредник
    public class ImageLoadingService
    {
        private readonly IFileStorage<ImageInfo[]> _storage;

        private readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        public ImageLoadingService(IFileStorage<ImageInfo[]> storage)
        {
            _storage = storage;
        }

        public async Task DownloadImageAsync(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var form = await request.ReadFormAsync();
            var images = form.Files;

            var count = 0;

            foreach( var image in images )
            {
                var extention = Path.GetExtension(image.FileName).ToLower();
                if(!_allowedExtensions.Contains(extention)) { continue; }
                await _storage.DownloadImageAsync(image);
                count++;
            }

            var text = count switch
            {
                1 => $"{count} изображение",
                <5 => $"{count} изображения",
                >=5 => $"{count} изображений"
            }; 

            await response.WriteAsync($"{text} загружено");
        }

        public async Task<PageInfo<ImageInfo>> GetImageAsync(int pageNumber, int pageSize)
        {
            var imagePairs = await _storage.GetImagesUrls();

            var elementsTotal = imagePairs.Count(); 

            var elementsToSkip = (pageNumber - 1) * pageSize;

            var imagesToTake = imagePairs
                .Skip(elementsToSkip)
                .Take(pageSize)
                .ToList();

            return new PageInfo<ImageInfo>
            {
                Files = imagesToTake,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalFilesCount = elementsTotal
            };
        }
    }
}