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

        private readonly IServiceProvider _serviceProvider;

        public DeletionBackgroundService(IStorage storage, 
            IOptions<StorageOptions> storageOptions,
            IOptions<DeletionOptions> deletionOptions,
            IServiceProvider serviceProvider)
        {
            _storage = storage;

            _storageOptions = storageOptions.Value;

            _deletionOptions = deletionOptions.Value;

            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = _deletionOptions.DelayTimeInSeconds;

            var numberToDeletion = _deletionOptions.OneCycleDeletionsCount;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var serviceScope = _serviceProvider.CreateScope();
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDBContext>();

                var query = dbContext.FilesToDeletion.AsNoTracking();

                var orderedQuery = query
                    .OrderBy(item => item.TrashedAt);

                var idList = await orderedQuery
                    .Take(numberToDeletion)
                    .Select(item => item.Id)
                    .ToListAsync(stoppingToken);

                await Parallel.ForEachAsync(idList,
                    new ParallelOptions
                    { 
                        MaxDegreeOfParallelism = _deletionOptions.ParallelsCount
                    }, 
                    async (id, stoppingToken) => await EraseAsync(dbContext,id,stoppingToken));
            }

        }

        private async Task EraseAsync(AppDBContext DBcontextScope, Guid guid, CancellationToken ct = default)
        {
            var id = guid.ToString();

            FileToDeletionModel model;

            try
            {
                model = await DBcontextScope.FilesToDeletion.FindAsync(guid) 
                    ?? throw new KeyNotFoundException();

                DBcontextScope.Remove(model);
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                await DBcontextScope.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                return;
            }


            try
            {
                bool imageDeletionSuccess = false;

                bool previewDeletionSuccess = false;

                await _storage.ExecuteAsync(id,
                    async storage =>
                    {
                        var imageDeletionTask = storage
                        .TryDeleteFileAsync(id,
                        _storageOptions.ImagesTrashDirectoryName,
                        ct);

                        var previewDeletionTask = storage
                        .TryDeleteFileAsync(id,
                        _storageOptions.PreviewsTrashDirectoryName,
                        ct);

                        await Task.WhenAll(imageDeletionTask, previewDeletionTask);

                        imageDeletionSuccess = await imageDeletionTask;

                        previewDeletionSuccess = await previewDeletionTask;
                    },
                    ct);

                if (!imageDeletionSuccess || !previewDeletionSuccess) throw new IOException();
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}
