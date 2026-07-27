using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using Microsoft.EntityFrameworkCore;

namespace ImageServer.Services.Storages
{
    public class DeletionBackgroundService : BackgroundService
    {
        private readonly IStorage _storage;

        private readonly StorageOptions _storageOptions;

        private readonly DeletionOptions _deletionOptions;

        private readonly IServiceProvider _serviceProvider;

        public DeletionBackgroundService(IStorage storage, 
            StorageOptions storageOptions,
            DeletionOptions deletionOptions,
            IServiceProvider serviceProvider)
        {
            _storage = storage;

            _storageOptions = storageOptions;

            _deletionOptions = deletionOptions;

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

                var orderedQuery = query.OrderBy(item => item.TrashedAt);

                var idList = await orderedQuery
                    .Take(numberToDeletion)
                    .Select(item => item.Id)
                    .ToListAsync(stoppingToken);
            }
        }
    }
}
