using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using ImageServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImageServer.Services.Storages
{
    public class DeletionBackgroundService : BackgroundService
    {
        private readonly IStorage _storage;

        private readonly StorageOptions _storageOptions;

        private readonly DeletionOptions _deletionOptions;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private class ErasingItemFailedException(string id, DeletionResult imageResult, DeletionResult previewResult)
            : Exception($"Ошибка при удалении файла с id: {id} из корзины.")
        {
            public DeletionResult ImageResult { get; } = imageResult;
            public DeletionResult PreviewResult { get; } = previewResult;
        }

        private readonly record struct DeletionResult(bool Success = true, Exception? Error = null);

        public DeletionBackgroundService(IStorage storage, 
            IOptions<StorageOptions> storageOptions,
            IOptions<DeletionOptions> deletionOptions,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _storage = storage;

            _storageOptions = storageOptions.Value;

            _deletionOptions = deletionOptions.Value;

            _contextFactory = contextFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = _deletionOptions.CycleTimeInSeconds;

            var numberToDeletion = _deletionOptions.OneCycleDeletionsCount;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    List<Guid> idList;
                    await using (var dbContext = await _contextFactory.CreateDbContextAsync(stoppingToken)) 
                    {
                        if (!await dbContext.Database.CanConnectAsync(stoppingToken)) throw new Exception($"Соединение с БД прервано");

                        if (!await dbContext.FilesToDeletion.IgnoreQueryFilters().AnyAsync(stoppingToken)) continue;

                        var query = dbContext.FilesToDeletion
                            .IgnoreQueryFilters()
                            .AsNoTracking();

                        idList = await query
                            .IgnoreQueryFilters()
                            .OrderBy(item => item.TrashedAt)
                            .Take(numberToDeletion)
                            .Select(item => item.Id)
                            .ToListAsync(stoppingToken);
                    }

                    await Parallel.ForEachAsync(idList,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = _deletionOptions.ParallelsCount,
                            CancellationToken = stoppingToken
                        },
                        async (id, ct) =>
                        {
                            try
                            {
                                await EraseAsync(_contextFactory, _storage, _storageOptions, id, ct);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                        });
                }
                catch(Exception)
                {
                    continue;
                }
            }
        }

        private static async Task EraseAsync(IDbContextFactory<AppDbContext> contextFactory, 
            IStorage storage, 
            StorageOptions storageOptions,
            Guid guid, 
            CancellationToken ct = default)
        {
            var id = guid.ToString();

            bool fileDeletionFailure = false;

            bool canDeleteFromDB = true;
            
            await using var dbContext = await contextFactory.CreateDbContextAsync(ct);

            try
            {
                await storage.ExecuteAsync(id,
                    async storage =>
                    {
                        var imageOpResult = await storage
                        .TryDeleteAsyncWithEx(id,
                        storageOptions.ImagesTrashDirectoryName,
                        CancellationToken.None);

                        var previewOpResult = await storage
                        .TryDeleteAsyncWithEx(id,
                        storageOptions.PreviewsTrashDirectoryName,
                        CancellationToken.None);

                        if(!imageOpResult.success || !previewOpResult.success)
                        {
                            throw new ErasingItemFailedException(id,
                                new DeletionResult(imageOpResult.success, imageOpResult.ex),
                                new DeletionResult(previewOpResult.success, previewOpResult.ex));
                        }
                    },
                    ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ErasingItemFailedException ex)
            {
                fileDeletionFailure = true;
                canDeleteFromDB = false;

                if (ex.ImageResult.Success == false
                    && ex.PreviewResult.Success == false
                    && ex.ImageResult.Error is FileNotFoundException 
                    && ex.PreviewResult.Error is FileNotFoundException) canDeleteFromDB = true;
                else if(ex.ImageResult.Success == true
                    && ex.PreviewResult.Success == false
                    && ex.PreviewResult.Error is FileNotFoundException) canDeleteFromDB = true;
                else if(ex.ImageResult.Success == false
                    && ex.ImageResult.Error is FileNotFoundException
                    && ex.PreviewResult.Success == true) canDeleteFromDB = true;
            }
            catch (Exception)
            {
                fileDeletionFailure = true;
                canDeleteFromDB = false;
            }

            if (fileDeletionFailure && !canDeleteFromDB)
            {
                try
                {
                    var item = await dbContext.FilesToDeletion
                        .IgnoreQueryFilters()
                        .Where(model => model.Id == guid)
                        .ExecuteUpdateAsync(model => model
                        .SetProperty(m => m.DeletionFailures, m => m.DeletionFailures + 1),
                        CancellationToken.None);

                    return;
                }
                catch (Exception)
                {
                    return;
                }
            }

            if (fileDeletionFailure == false || canDeleteFromDB == true)
            {
                try
                {
                    await dbContext.FilesToDeletion
                        .IgnoreQueryFilters()
                        .Where(item => item.Id == guid)
                        .ExecuteDeleteAsync(CancellationToken.None);

                    return;
                }
                catch (Exception)
                {
                    return;
                }
            }

        }
    }
}
