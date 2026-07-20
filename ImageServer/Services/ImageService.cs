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

        private readonly StorageOptions _storageOptions;

        private readonly string _imagesTrashRoot; 

        private readonly string _previewsTrashRoot;

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
            IOptions<StorageOptions> storageOptions)
        {
            _DBcontext = DBcontext;

            _processor = processor;

            _storage = storage;

            _serviceOptions = serviceOptions.Value;

            _storageOptions = storageOptions.Value;

            _imagesTrashRoot = Path.Combine(_storageOptions.MainPath, "ImagesTrash");

            _previewsTrashRoot = Path.Combine(_storageOptions.MainPath, "PreviewsTrash");
        }

        private static ImageModelFilterDelegate CreateFilter<TSelectorField>
            (Expression<Func<ImageModel, TSelectorField>> selector) =>
            (query, ascending) => ascending
            ? query.OrderBy(selector)
            : query.OrderByDescending(selector);

        public async Task<ServiceResult<SavedResult>> SaveImagesAsync(IFormFileCollection images, CancellationToken ct)
        {
            var successful = new ConcurrentBag<ImageModel>();

            var failed = new ConcurrentBag<string>();

            var token = ct;

            await Parallel.ForEachAsync(images,

                new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = _serviceOptions.ParallelismDegree 
                },

                async (image,token) =>
                {
                    try
                    {
                        var imageModel = await ProcessAsync(image, token);

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

            var imageSavingTask = _storage.SaveFileAsync(imageStream, imgName, _storageOptions.ImagesDirectoryName);

            var previewSavingTask = _storage.SaveFileAsync(previewStream, thumbName, _storageOptions.PreviewsDirectoryName);

            await Task.WhenAll(imageSavingTask, previewSavingTask);

            return new ImageModel(id);
        }

        public ServiceResult<Stream> GetImage(string id) 
        {
            Stream imageStream;

            if(_storage.TryGetFile(id,_storageOptions.ImagesDirectoryName, out var imgStream))
            {
                imageStream = imgStream!;
            }
            else return ServiceResult<Stream>.Fail($"Изображение с id:{id} не найдено");

            return ServiceResult<Stream>.Ok(imageStream);
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
                    _storageOptions.ImagesDirectoryName, 
                    _storageOptions.PreviewsDirectoryName,
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
            Guid guid;

            if (Guid.TryParse(id, out var parsedId)) guid = parsedId;
            else return ServiceResult.Fail("Неверный формат ID");

            #region Удаление из базы данных(в памяти)

            try
            {
                var image = await _DBcontext.Images.FindAsync(guid, ct)
                   ?? throw new InvalidOperationException($"Изображение с id:{id} не найдено.");

                _DBcontext.Images.Remove(image);
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail(ex.Message);
            }

            #endregion

            #region Перемещение файлов в корзину 

            var imageTrashedSuccess = await _storage.TryMoveFile(id, _storageOptions.ImagesDirectoryName, _imagesTrashRoot, ct); 
            var previewTrashedSuccess = await _storage.TryMoveFile(id, _storageOptions.PreviewsDirectoryName, _previewsTrashRoot, ct);

            bool fault = false;

            if (!imageTrashedSuccess)
            {
                fault = true;
                if (previewTrashedSuccess)
                    await _storage.MoveFile(id, _previewsTrashRoot, _storageOptions.PreviewsDirectoryName, ct);
            }
            if (!previewTrashedSuccess)
            {
                fault = true;
                if (imageTrashedSuccess)
                    await _storage.MoveFile(id,_imagesTrashRoot, _storageOptions.ImagesDirectoryName, ct);
            }

            if (fault) return ServiceResult.Fail($"Ошибка перемещения файла c id:{id} в коризну");

            #endregion

            #region Добавление модели удаления в базу данных

            var deletionModel = new FileToDeletionModel(guid);

            try
            {
                await _DBcontext.AddAsync(deletionModel, ct);
            }
            catch (Exception ex)
            {
                await revertFileChanges();
                return ServiceResult.Fail($"Ошибка добавления модели удаления: {ex.Message}");
            }

            #endregion

            #region Внесение изменений в базу данных

            try
            {
                await _DBcontext.SaveChangesAsync(ct);
            }
            catch(Exception ex)
            {
                await revertFileChanges();
                return ServiceResult.Fail($"Ошибка сохранения изменений в базе данных: {ex.Message}");
            }

            #endregion

            return ServiceResult.Ok();

            async Task revertFileChanges()
            {
                await _storage.MoveFile(id, _imagesTrashRoot, _storageOptions.ImagesDirectoryName, CancellationToken.None);
                await _storage.MoveFile(id, _previewsTrashRoot, _storageOptions.PreviewsDirectoryName, CancellationToken.None);
            }
        }

        public async Task<ServiceResult> UpdateAsync(string id, IImageUpdateCommand command, CancellationToken ct)
        {
            try
            {
                Guid guid;

                if (Guid.TryParse(id, out var parsedId)) guid = parsedId;
                else return ServiceResult.Fail("Неверный формат ID");

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
