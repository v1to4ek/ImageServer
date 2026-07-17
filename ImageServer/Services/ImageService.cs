using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using ImageServer.DTOs;
using ImageServer.Enums;
using ImageServer.Models;
using ImageServer.Services.Processors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace ImageServer.Services
{
    public class ImageService
    {
        private readonly AppDBContext _DBcontext;

        private readonly IImageProcessor _processor;

        private readonly IStorage _storage;

        private readonly ImgServiceOptions _serviceOptions;

        private readonly string _imagesDirectoryName;

        private readonly string _previewsDirectoryName;

        private delegate IOrderedQueryable<ImageModel> ImageModelFilterDelegate(IQueryable<ImageModel> query, bool ascending);

        private static readonly Dictionary<OrderingSelectors, ImageModelFilterDelegate> _orderingSelectors = new()
        {
            [OrderingSelectors.Date] = CreateFilter(model => model.CreatedAt),
            [OrderingSelectors.Name] = CreateFilter(model => model.Name),
            [OrderingSelectors.Favourite] = CreateFilter(model => model.IsFavourite)
        };

        public ImageService(AppDBContext DBcontext, 
            IImageProcessor processor, 
            IStorage storage, 
            IOptions<ImgServiceOptions> serviceOptions, 
            IOptions<StorageOptions> storageOptions,
            IServiceProvider serviceProvider)
        {
            _DBcontext = DBcontext;

            _processor = processor;

            _storage = storage;

            _serviceOptions = serviceOptions.Value;

            _imagesDirectoryName = storageOptions.Value.ImagesDirectoryName;

            _previewsDirectoryName = storageOptions.Value.PreviewsDirectoryName;
        }

        private static ImageModelFilterDelegate CreateFilter<TSelectorField>
            (Expression<Func<ImageModel, TSelectorField>> selector) =>
            (query, ascending) => ascending
            ? query.OrderBy(selector)
            : query.OrderByDescending(selector);

        public async Task<ServiceResult<SavedResult>> SaveImagesAsync(IFormFileCollection images)
        {
            var successful = new ConcurrentBag<ImageModel>();

            var failed = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(images,

                new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = _serviceOptions.ParallelismDegree 
                },

                async (image,ct) =>
                {
                    try
                    {
                        var imageModel = await ProcessAsync(image, ct);

                        successful.Add(imageModel);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{image.Name}: {ex.Message}");
                    }
                });

            if(!successful.IsEmpty)
            {
                await _DBcontext.AddRangeAsync(successful);

                await _DBcontext.SaveChangesAsync();
            }

            var savedResult = new SavedResult(
                successful.Count,
                failed.ToList());

            return ServiceResult<SavedResult>.Ok(savedResult);
        }

        private async Task<ImageModel> ProcessAsync(IFormFile image, CancellationToken ct)
        {
            var isValid = await _processor.ProcessAsync<ExtentionValidationProcessor, bool, string>(image.FileName, ct);

            if (!isValid) throw new InvalidOperationException($"Недопустимый формат файла: {Path.GetExtension(image.FileName)}");

            var id = Guid.NewGuid();
            var imgName = $"{id}.webp";
            var thumbName = $"{id}.webp";

            await using var sourceStream = new MemoryStream();

            await image.CopyToAsync(sourceStream, ct);

            sourceStream.Position = 0;

            await using var imageStream = await _processor.ProcessAsync<ImageConversionProcessor, Stream, Stream>(sourceStream, ct);

            sourceStream.Position = 0;

            await using var previewStream = await _processor.ProcessAsync<PreviewConversionProcessor, Stream, Stream>(sourceStream, ct);

            var imageSavingTask = _storage.SaveFileAsync(imageStream, imgName, _imagesDirectoryName);

            var previewSavingTask = _storage.SaveFileAsync(previewStream, thumbName, _previewsDirectoryName);

            await Task.WhenAll(imageSavingTask, previewSavingTask);

            return new ImageModel(id);
        }

        public ServiceResult<Stream> GetImage(string id) 
        {
            try
            {
                var imgStream = _storage.GetFile(id, _imagesDirectoryName);

                return ServiceResult<Stream>.Ok(imgStream);
            }
            catch(FileNotFoundException ex)
            {
                return ServiceResult<Stream>.Fail(ex.Message);
            }
        }

        public async Task<ServiceResult<PagedResponse<ImageDTO>>> GetPagedResultAsync(PagedRequest request, CancellationToken ct)
        {
            var imgQuery = _DBcontext.Images.AsNoTracking();
            
            var isAscending = request.OrderingType == OrderingTypes.Ascending;

            var orderingSelector = request.OrderingSelector;

            ImageModelFilterDelegate filterDelegate = null!;

            if (_orderingSelectors.TryGetValue(orderingSelector, out var resultDelegate))
            {
                filterDelegate = resultDelegate;
            }
            else
            {
                filterDelegate = _orderingSelectors[0];
            }

            var orderedQuery = filterDelegate(imgQuery, isAscending);

            var totalCount = await imgQuery.CountAsync(ct);

            var itemsToTake = await orderedQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(img =>
                new ImageDTO(
                    img.Id.ToString(), 
                    _imagesDirectoryName, 
                    _previewsDirectoryName,
                    img.Name, 
                    img.IsFavourite,
                    img.CreatedAt))
                .ToListAsync(ct);

            var response = new PagedResponse<ImageDTO>
                (itemsToTake,
                totalCount,
                request.PageNumber,
                request.PageSize);

            return ServiceResult<PagedResponse<ImageDTO>>.Ok(response);
        }

        public async Task<ServiceResult> DeleteAsync(string id, CancellationToken ct)
        {
            try
            {
                var guid = Guid.Parse(id);

                var image = await _DBcontext.Images.FindAsync(guid) ?? throw new Exception("Сущность не найдена");

                _DBcontext.Images.Remove(image);

                await _DBcontext.SaveChangesAsync();

                _storage.DeleteFile(id, _imagesDirectoryName);

                _storage.DeleteFile(id, _previewsDirectoryName);

                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail(ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateAsync(string id, IImageUpdateCommand command, CancellationToken ct)
        {
            try
            {
                var guid = Guid.Parse(id);

                var image = await _DBcontext.Images.FindAsync(guid, ct) ?? throw new Exception("Сущность не найдена");

                command.Execute(image);

                await _DBcontext.SaveChangesAsync(ct);

                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail(ex.Message);
            }
        }
    }
}
