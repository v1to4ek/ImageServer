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

namespace ImageServer.Services
{
    public class ImageService
    {
        private readonly AppDbContext _dbContext;

        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        private readonly IImageProcessor _processor;

        private readonly IStorage _storage;

        private readonly ImgServiceOptions _serviceOptions;

        private readonly StorageOptions _storageOptions;

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

        public ImageService(AppDbContext DBcontext, 
            IDbContextFactory<AppDbContext> dbContextFactory,
            IImageProcessor processor, 
            IStorage storage, 
            IOptions<ImgServiceOptions> serviceOptions, 
            IOptions<StorageOptions> storageOptions)
        {
            _dbContext = DBcontext;

            _dbContextFactory = dbContextFactory;

            _processor = processor;

            _storage = storage;

            _serviceOptions = serviceOptions.Value;

            _storageOptions = storageOptions.Value;
        }

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
                await _dbContext.AddRangeAsync(successful, ct);

                await _dbContext.SaveChangesAsync(ct);
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
        
        public async Task<ServiceResult<PagedResponse<ImageDTO>>> GetImagesPagedAsync(PagedImagesRequest request, CancellationToken ct)
        {
            var serviceRequest = new PagedImagesServiceRequest(request.PageNumber,
                request.PageSize,
                request.OrderingSelector,
                request.OrderingType,
                _storageOptions);

            var serviceResult = await GetPagedAsync(serviceRequest, ct);

            return serviceResult;
        }

        public async Task<ServiceResult<PagedResponse<TrashedDTO>>> GetTrashedPagedAsync(PagedTrashedRequest request, CancellationToken ct)
        {
            var serviceRequest = new PagedTrashedServiceRequest(request.PageNumber,
                request.PageSize,
                request.OrderingSelector,
                request.OrderingType);

            var serviceResult = await GetPagedAsync(serviceRequest, ct);

            return serviceResult;
        }

        private async Task<ServiceResult<PagedResponse<TDto>>> GetPagedAsync<TDBModel,TDto, TOrderingSelector>
            (PagedServiceRequestBase<TDBModel, TDto, TOrderingSelector> request,
            CancellationToken ct)
            where TDBModel : class
            where TDto : class
            where TOrderingSelector : struct, Enum
        {
            if (request.PageNumber <= 0 || request.PageSize <= 0)
            {
                return ServiceResult<PagedResponse<TDto>>.Fail("Номер страницы и размер страницы должны быть больше нуля");
            }

            if(request.PageSize > _serviceOptions.MaxAllowedPageSize)
            {
                return ServiceResult<PagedResponse<TDto>>.Fail($"Размер страницы не может превышать {_serviceOptions.MaxAllowedPageSize}");
            }

            var isAscending = request.OrderingType == OrderingType.Ascending;

            var orderingSelector = request.OrderingSelector;

            var filterDelegate = request.FilterDictionary
                .GetValueOrDefault(orderingSelector, request.DefaultFilter);

            try
            {
                var query = _dbContext.Set<TDBModel>().AsNoTracking();

                var orderedQuery = filterDelegate(query, isAscending);

                var total = await query.CountAsync(ct);

                var takenItems = await orderedQuery
                    .Skip((request.PageNumber -1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(request.DtoSelectorFactory)
                    .ToListAsync(ct);

                var response = new PagedResponse<TDto>
                    (takenItems,
                    total,
                    request.PageNumber,
                    request.PageSize);

                return ServiceResult<PagedResponse<TDto>>.Ok(response);
            }
            catch (Exception ex)
            {
                return ServiceResult<PagedResponse<TDto>>.Fail($"Ошибка получения данных: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteOneAsync(string id, CancellationToken ct)
            => await DeleteCoreAsync(id, _dbContext, ct);

        public async Task<ServiceResult> RestoreOneAsync(string id, CancellationToken ct)
            => await RestoreCoreAsync(id, _dbContext, ct);

        public async Task<ServiceResult<RelocationResponse>> DeleteManyAsync(List<string> ids, CancellationToken ct)
            => await RelocateManyAsync(ids, DeleteCoreAsync, ct);

        public async Task<ServiceResult<RelocationResponse>> RestoreManyAsync(List<string> ids, CancellationToken ct)
            => await RelocateManyAsync(ids, RestoreCoreAsync, ct);

        private async Task<ServiceResult<RelocationResponse>> RelocateManyAsync(List<string> ids,
            Func<string, AppDbContext, CancellationToken, Task<ServiceResult>> operationFactory,
            CancellationToken ct)
        {
            var filesCount = ids.Count;

            IReadOnlyDictionary<string, ServiceResult> relocationResult;

            if(filesCount == 0) return ServiceResult<RelocationResponse>.Fail("Список идентификаторов пуст");

            if(filesCount > _serviceOptions.MaxAllowedBatchSize) return ServiceResult<RelocationResponse>.Fail($"Размер пакета не может превышать {_serviceOptions.MaxAllowedBatchSize}");

            if(ids.Distinct().ToList().Count != ids.Count) return ServiceResult<RelocationResponse>.Fail("Список идентификаторов содержит дубликаты");

            bool shouldParallel = filesCount > _serviceOptions.MaxSequentalBatchSize;

            if (shouldParallel)
            {
                relocationResult = await ExecuteParallelRelocAsync(ids, operationFactory, ct);
            }
            else
            {
                relocationResult = await ExecuteSequentialRelocAsync(ids, operationFactory, ct);
            }

            var successList = relocationResult
                .Where(item => item.Value.IsSuccess)
                .Select(item => item.Key)
                .ToList();

            var failedList = relocationResult
                .Where(item => !item.Value.IsSuccess)
                .Select(item => item.Key)
                .ToList();

            return ServiceResult<RelocationResponse>.Ok(new RelocationResponse(successList, failedList));
        }

        private async Task<IReadOnlyDictionary<string, ServiceResult>> ExecuteParallelRelocAsync(List<string> ids,
            Func<string, AppDbContext, CancellationToken, Task<ServiceResult>> operationFactory,
            CancellationToken ct)
        {
            var resultDictionary = new ConcurrentDictionary<string, ServiceResult>();

            await Parallel.ForEachAsync(ids,
                new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = _serviceOptions.ParallelismDegree
                },
                async (id, token) =>
                {
                    try
                    {
                        await using var dbContextScope = await _dbContextFactory.CreateDbContextAsync(token);
                        var result = await operationFactory(id, dbContextScope, token);
                        var success = resultDictionary.TryAdd(id, result);
                        if (!success) throw new InvalidOperationException($"Ошибка добавления результата операции для ID:{id}.Возможны повторяющиеся идентификаторы");
                    }
                    catch (InvalidOperationException ex)
                    {
                        var tempGuid = Guid.NewGuid().ToString();
                        resultDictionary.TryAdd($"Id с ошибкой:{id}.Временный id:{tempGuid}", ServiceResult.Fail(ex.Message));
                    }
                });

            return resultDictionary;
        }

        private async Task<IReadOnlyDictionary<string, ServiceResult>> ExecuteSequentialRelocAsync(List<string> ids,
            Func<string, AppDbContext, CancellationToken, Task<ServiceResult>> operationFactory,
            CancellationToken ct)
        {
            var resultDictionary = new Dictionary<string, ServiceResult>();

            foreach(var id in ids)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await operationFactory(id, _dbContext, ct);
                    var success = resultDictionary.TryAdd(id, result);
                    if(!success) throw new InvalidOperationException($"Ошибка добавления результата операции для ID:{id}.Возможны повторяющиеся идентификаторы");
                }
                catch(InvalidOperationException ex)
                {
                    var tempGuid = Guid.NewGuid().ToString();
                    resultDictionary.Add($"Id с ошибкой:{id}.Временный id:{tempGuid}", ServiceResult.Fail(ex.Message));
                }
            }

            return resultDictionary;
        }

        private async Task<ServiceResult> DeleteCoreAsync(string id, AppDbContext dbContext, CancellationToken ct)
        {
            var paths = RelocationPaths.ToTrash(
                _storageOptions.ImagesDirectoryName,
                _storageOptions.PreviewsDirectoryName,
                _storageOptions.ImagesTrashDirectoryName,
                _storageOptions.PreviewsTrashDirectoryName);

            var operationResult = await RelocateAtomicAsync(id, paths,
                async (context, guid) => (await context.Images.FindAsync(guid, ct))!,
                guid => new FileToDeletionModel(guid),
                dbContext,
                ct);

            return operationResult;
        }

        private async Task<ServiceResult> RestoreCoreAsync(string id, AppDbContext dbContext, CancellationToken ct)
        {
            var paths = RelocationPaths.FromTrash(
                _storageOptions.ImagesDirectoryName,
                _storageOptions.PreviewsDirectoryName,
                _storageOptions.ImagesTrashDirectoryName,
                _storageOptions.PreviewsTrashDirectoryName);

            return await RelocateAtomicAsync(id, paths,
                async (context, guid) => (await context.FilesToDeletion.FindAsync(guid, ct))!,
                guid => new ImageModel(guid),
                dbContext,
                ct);
        }

        private async Task<ServiceResult> RelocateAtomicAsync<TModelFrom, TModelTo>(string id,
            RelocationPaths relocationPaths,
            Func<AppDbContext, Guid, ValueTask<TModelFrom>> modelToDeleteFactory,
            Func<Guid, TModelTo> modelToSaveFactory,
            AppDbContext dbContext,
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
                var modelToDelete = await modelToDeleteFactory(dbContext, guid)
                    ?? throw new KeyNotFoundException($"Запись с ID:{id} не найдена");

                dbContext.Set<TModelFrom>().Remove(modelToDelete);
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
                await dbContext.Set<TModelTo>().AddAsync(relocatedModel, ct);
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
                await dbContext.SaveChangesAsync(ct);
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

                var image = await _dbContext.Images.FindAsync(guid, ct) ?? throw new Exception("Сущность не найдена");

                command.Execute(image);

                await _dbContext.SaveChangesAsync(ct);

                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail(ex.Message);
            }
        }
    }
}
