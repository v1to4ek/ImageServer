using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using ImageServer.DTOs;
using ImageServer.Enums;
using ImageServer.Models;
using ImageServer.Services.Processors;
using ImageServer.Services.Storages;
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

        private delegate IOrderedQueryable<ImageModel> ImageModelFilterDelegate(IQueryable<ImageModel> query, bool ascending);

        private static readonly Dictionary<OrderingSelectors, ImageModelFilterDelegate> _orderingSelectors = new()
        {
            [OrderingSelectors.Date] = CreateFilter(model => model.CreatedAt),
            [OrderingSelectors.Name] = CreateFilter(model => model.Name),
            [OrderingSelectors.Favourite] = CreateFilter(model => model.IsFavourite)
        };

        private readonly record struct RelocationPaths
        {
            public (string From, string To) ImagesPaths { get; init; }

            public (string From, string To) PreviewsPaths { get; init; }

            public static RelocationPaths ToTrash(string imagesDirectoryRoot,
                string previewsDirectoryRoot, 
                string imagesTrashRoot,
                string previewsTrashRoot) => new()
            {
                ImagesPaths = (imagesDirectoryRoot, imagesTrashRoot),
                PreviewsPaths = (previewsDirectoryRoot, previewsTrashRoot)
            };

            public static RelocationPaths FromTrash(string imagesDirectoryRoot, 
                string previewsDirectoryRoot, 
                string imagesTrashRoot, 
                string previewsTrashRoot) => new()
            {
                ImagesPaths = (imagesTrashRoot, imagesDirectoryRoot),
                PreviewsPaths = (previewsTrashRoot, previewsDirectoryRoot)
            };
        }

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
        }

        private static ImageModelFilterDelegate CreateFilter<TSelectorField>
            (Expression<Func<ImageModel, TSelectorField>> selector) =>
            (query, ascending) => ascending
            ? query.OrderBy(selector)
            : query.OrderByDescending(selector);

        public async Task<ServiceResult<SavedResult>> SaveAsync(IFormFileCollection images, CancellationToken ct)
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
                await _DBcontext.AddRangeAsync(successful, ct);

                await _DBcontext.SaveChangesAsync(ct);
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
            var imgName = id.ToString();
            var thumbName = id.ToString(); 

            await using var sourceStream = new MemoryStream();

            await image.CopyToAsync(sourceStream, ct);

            sourceStream.Position = 0;

            await using var imageStream = await _processor.ProcessAsync<ImageConversionProcessor, Stream, Stream>(sourceStream, ct);

            sourceStream.Position = 0;

            await using var previewStream = await _processor.ProcessAsync<PreviewConversionProcessor, Stream, Stream>(sourceStream, ct);

            var imageSavingTask = _storage.SaveFileAsync(imageStream, imgName, _storageOptions.ImagesDirectoryName, ct);

            var previewSavingTask = _storage.SaveFileAsync(previewStream, thumbName, _storageOptions.PreviewsDirectoryName, ct);

            await Task.WhenAll(imageSavingTask, previewSavingTask);

            return new ImageModel(id);
        }

        public async Task<ServiceResult<Stream>> GetAsync(string id, CancellationToken ct)
        {
            Stream imageStream;

            var result = await _storage.TryGetAsync(id, _storageOptions.ImagesDirectoryName, ct);

            if (result.success)
            {
                imageStream = result.stream!;
            }
            else return ServiceResult<Stream>.Fail($"Изображение с id:{id} не найдено");

            return ServiceResult<Stream>.Ok(imageStream);
        }

        public async Task<ServiceResult<PagedResponse<ImageDTO>>> GetPagedResultAsync(PagedRequest request, CancellationToken ct)
        {
            if(request.PageNumber <= 0 || request.PageSize <= 0)
            {
                return ServiceResult<PagedResponse<ImageDTO>>.Fail("Номер страницы и размер страницы должны быть больше нуля");
            }

            if(request.PageSize > _serviceOptions.MaxAllowedPageSize)
            {
                return ServiceResult<PagedResponse<ImageDTO>>.Fail($"Размер страницы не может превышать {_serviceOptions.MaxAllowedPageSize}");
            }

            var isAscending = request.OrderingType == OrderingTypes.Ascending;

            var orderingSelector = request.OrderingSelector;

            ImageModelFilterDelegate filter = null!;

            if (_orderingSelectors.TryGetValue(orderingSelector, out var resultDelegate))
            {
                filter = resultDelegate;
            }
            else
            {
                filter = _orderingSelectors[0];
            }

            try
            {
                var imgQuery = _DBcontext.Images.AsNoTracking();

                var orderedQuery = filter(imgQuery, isAscending);

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
            catch (Exception ex)
            {
                return ServiceResult<PagedResponse<ImageDTO>>.Fail($"Ошибка получения данных: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(string id, CancellationToken ct)
        {
            var paths = RelocationPaths.ToTrash(_storageOptions.ImagesDirectoryName,
                _storageOptions.PreviewsDirectoryName,
                _storageOptions.ImagesTrashDirectoryName,
                _storageOptions.PreviewsTrashDirectoryName);

            var operationResult = await RelocateAtomicAsync(id, paths,
                async (context, guid) => (await context.Images.FindAsync(guid, ct))!,
                guid => new FileToDeletionModel(guid),
                ct);

            return operationResult;
        }

        public async Task<ServiceResult> RestoreAsync(string id, CancellationToken ct)
        {
            var paths = RelocationPaths.FromTrash(_storageOptions.ImagesDirectoryName,
                _storageOptions.PreviewsDirectoryName,
                _storageOptions.ImagesTrashDirectoryName,
                _storageOptions.PreviewsTrashDirectoryName);

            var operationResult = await RelocateAtomicAsync(id, paths,
                async (context, guid) => (await context.FilesToDeletion.FindAsync(guid, ct))!,
                guid => new ImageModel(guid),
                ct);

            return operationResult;
        }

        private async Task<ServiceResult> RelocateAtomicAsync<TModelFrom,TModelTo>(string id,
            RelocationPaths relocationPaths,
            Func<AppDBContext, Guid, ValueTask<TModelFrom>> modelToDeleteFactory,
            Func<Guid, TModelTo> modelToSaveFactory,
            CancellationToken ct)
            where TModelFrom : class
            where TModelTo : class
        {
            Guid guid;

            if(Guid.TryParse(id, out var parsedId)) guid = parsedId;
            else return ServiceResult.Fail("Неверный формат ID");

            #region Удаление из базы данных(в памяти)

            try
            {
                var modelToDelete = await modelToDeleteFactory(_DBcontext, guid)
                    ?? throw new KeyNotFoundException($"Запись с ID:{id} не найдена");

                _DBcontext.Set<TModelFrom>().Remove(modelToDelete);
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Ошибка удаления модели из базы данных: {ex.Message}. Операция не была выполнена.");
            }

            #endregion

            #region Перемещение файлов в корзину 

            try
            {
                await _storage.ExecuteAsync(id, async storage =>
                {
                    var imageRelocatedTask = storage
                    .TryMoveAsync(id,
                    relocationPaths.ImagesPaths.From,
                    relocationPaths.ImagesPaths.To,
                    ct);

                    var previewRelocaredTask = storage
                    .TryMoveAsync(id,
                    relocationPaths.PreviewsPaths.From,
                    relocationPaths.PreviewsPaths.To,
                    ct);

                    await Task.WhenAll(imageRelocatedTask, previewRelocaredTask);

                    var imageRelocatedSuccess = await imageRelocatedTask;
                    var previewRelocaredSuccess = await previewRelocaredTask;

                    if (!imageRelocatedSuccess || !previewRelocaredSuccess)
                    {

                        if (imageRelocatedSuccess)
                            await storage
                            .MoveAsync(id,
                            relocationPaths.ImagesPaths.To,
                            relocationPaths.ImagesPaths.From,
                            CancellationToken.None);

                        if (previewRelocaredSuccess)
                            await storage
                            .MoveAsync(id,
                            relocationPaths.PreviewsPaths.To,
                            relocationPaths.PreviewsPaths.From,
                            CancellationToken.None);

                        throw new IOException($"Ошибка перемещения файла c ID:{id}. Откат действий ФС и БД.");
                    }
                },
                ct);
            }
            catch(Exception ex)
            {
                return ServiceResult.Fail(ex.Message);
            }

            #endregion

            #region Добавление модели в другую базу данных

            try
            {
                var relocatedModel = modelToSaveFactory(guid);
                await _DBcontext.Set<TModelTo>().AddAsync(relocatedModel, ct);
            }
            catch(Exception ex)
            {
                await RollbackFileChangesAsync();
                return ServiceResult.Fail($"Ошибка добавления модели в базу назначения: {ex.Message}. Откат действий ФС и БД.");
            }

            #endregion

            #region Внесение изменений в базу данных

            try
            {
                await _DBcontext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                await RollbackFileChangesAsync();
                return ServiceResult.Fail($"Ошибка сохранения изменений в базе данных: {ex.Message}. Откат действий ФС и БД.");
            }

            #endregion

            return ServiceResult.Ok();

            #region Локальный метод отката изменений в файловой системе 

            async Task RollbackFileChangesAsync()
                => await _storage.ExecuteAsync(id,
                async storage =>
                {
                    var imageReverted = storage
                    .MoveAsync(id,
                    relocationPaths.ImagesPaths.To,
                    relocationPaths.ImagesPaths.From,
                    CancellationToken.None);

                    var previewReverted = storage
                    .MoveAsync(id,
                    relocationPaths.PreviewsPaths.To,
                    relocationPaths.PreviewsPaths.From,
                    CancellationToken.None);

                    await Task.WhenAll(imageReverted, previewReverted);
                },
                CancellationToken.None);

            #endregion
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
