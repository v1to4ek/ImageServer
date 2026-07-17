using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using Microsoft.Extensions.Options;

namespace ImageServer.Services.Deleters
{
    public class DirectDeleter : IImageDeleter
    {
        private readonly AppDBContext _appDBContext;

        private readonly IStorage _storage;

        private readonly StorageOptions _storageOptions;

        private readonly string _trashRoot;

        public DirectDeleter(AppDBContext appDBContext,
            IStorage storage, 
            IOptions<StorageOptions> storageOptions)
        {
            _appDBContext = appDBContext;

            _storage = storage;

            _storageOptions = storageOptions.Value;

            _trashRoot = Path.Combine(_storageOptions.MainPath, "trash");
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var guid = Guid.Parse(id);

            var databaseOperationSuccess = true;

            var storageOperationSuccess = true;

            try
            {
                var image = await _appDBContext.Images.FindAsync(guid, cancellationToken)
                    ?? throw new InvalidOperationException($"Изображение с {id} не найдено.");

                _appDBContext.Images.Remove(image);
            }
            catch (Exception)
            {
                databaseOperationSuccess = false;
            }

            await using var imageStream = _storage.GetFile(id, _storageOptions.ImagesDirectoryName);
            await using var previewStream = _storage.GetFile(id, _storageOptions.PreviewsDirectoryName);

            var imageFilePath = $"{Path.Combine(
                _storageOptions.MainPath,
                _storageOptions.ImagesDirectoryName,
                id)}.webp";

            var previewFilePath = $"{Path.Combine(
                _storageOptions.MainPath,
                _storageOptions.PreviewsDirectoryName,
                id)}.webp";

            if (databaseOperationSuccess)
            {
                try
                {
                    _storage.DeleteFile(id, _storageOptions.ImagesDirectoryName);
                    _storage.DeleteFile(id, _storageOptions.PreviewsDirectoryName);
                }
                catch (Exception ex)
                {
                    //if (ex is FileNotFoundException) 
                    storageOperationSuccess = false;
                }

            }

            if(databaseOperationSuccess && storageOperationSuccess)
            {
                try
                {
                    await _appDBContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception)
                {
                    
                }
            }
        }
    }
}
