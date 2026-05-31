using ImageServer.Database;
using ImageServer.DTOs;
using ImageServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

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
        public async Task<int> SaveImagesAsync(IFormFileCollection images)
        {
            var resultModels = new ConcurrentBag<ImageModel>();

            await Parallel.ForEachAsync(images,
                new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = 4 
                },
                async (image,ct) =>
                {
                    var imageModel = await ProcessAsync(image,ct);
                    resultModels.Add(imageModel);
                });

            await _DBcontext.AddRangeAsync(resultModels);

            await _DBcontext.SaveChangesAsync();

            return resultModels.Count;
        }

        private async Task<ImageModel> ProcessAsync(IFormFile image, CancellationToken ct)
        {
            var extension = Path.GetExtension(image.FileName).ToLower();
            var id = Guid.NewGuid();
            var imgName = $"{id}{extension}";
            var thumbName = $"{id}.webp";

            await using var originalStream = image.OpenReadStream();
            await using var previewSourceStream = new MemoryStream();
            await originalStream.CopyToAsync(previewSourceStream);

            previewSourceStream.Position = 0;
            originalStream.Position = 0;

            await using var previewStream = await _processor.GenerateThumbnailAsync(previewSourceStream, 300, 300);

            var imgUrl = _storage.SaveAsync(originalStream, imgName, "images");
            var thumbUrl = _storage.SaveAsync(previewStream, thumbName, "previews");

            await Task.WhenAll(imgUrl, thumbUrl);

            return new ImageModel(id , await imgUrl, await thumbUrl);
        }

        //получние полноценной картинки
        public Stream GetImage(string id) 
        {
            var imgStream = _storage.GetFile(id, "images");

            return imgStream;
        }

        //получение страницы превью
        public async Task<PagedResponse<ImageDTO>> GetPagedResultAsync(PagedRequest request)
        {
            var imgQuery = _DBcontext.Images
                .AsNoTracking()
                .OrderByDescending(img => img.CreatedAt);

            var totalCount = await imgQuery.CountAsync();

            var itemsToTake = await imgQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(img => new ImageDTO(img.ImageUrl,img.PreviewUrl))
                .ToListAsync();

            return new PagedResponse<ImageDTO>(
                itemsToTake,
                totalCount,
                request.PageNumber,
                request.PageSize);
        }

        //удаление картинки
        public async Task DeleteAsync(string id)
        {
            var image = await _DBcontext.Images.FindAsync(id) ?? throw new Exception("Сущность не найдена");

            _DBcontext.Images.Remove(image);

            await _DBcontext.SaveChangesAsync();

            _storage.DeleteFile(id, "images");

            _storage.DeleteFile(id, "previews");
        }
    }
}
