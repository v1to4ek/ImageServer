using ImageServer.Database;
using ImageServer.Models;

namespace ImageServer.Services
{
    public class ImageService
    {
        private readonly AppDBContext _DBcontext;

        private readonly IImageProcessor _processor;

        private readonly IStorage _storage;

        public ImageService(AppDBContext DBcontext, IImageProcessor processor, IStorage storage)
        {
            _DBcontext = DBcontext;
            _processor = processor;
            _storage = storage;
        }

        //загрузка картинок
        public async Task<int> SaveImageAsync(IFormFileCollection images)
        {
            int count = 0;

            foreach (var image in images)
            {
                var extention = Path.GetExtension(image.FileName).ToLower();

                var id = Guid.NewGuid();

                var imgName = $"{id}{extention}";
                var thumbName = $"{id}.webp";

                await using var originalStream = image.OpenReadStream();
                await using var previewSourceStream = new MemoryStream();
                await originalStream.CopyToAsync(previewSourceStream);

                previewSourceStream.Position = 0;

                await using var previewStream = await _processor.GenerateThumbnailAsync(previewSourceStream, 300, 300);

                originalStream.Position = 0;

                var imgUrl = await _storage.SaveAsync(originalStream, imgName, "images");
                var thumbUrl = await _storage.SaveAsync(previewStream, thumbName, "previews");

                var imgModel = new ImageModel(id, imgUrl, thumbUrl);

                await _DBcontext.AddAsync(imgModel);

                count++;
            }

            await _DBcontext.SaveChangesAsync();
            return count;
        }

        //получние полноценной картинки
        public async Task GetImageAsync()
        {

        }

        //получение превью
        public async Task GetThumbAsync()
        {

        }

        //удаление картинки
        public async Task DeleteAsync()
        {

        }
    }
}
