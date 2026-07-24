using ImageServer.Abstractions;
using ImageServer.Configuration;
using Microsoft.Extensions.Options;

namespace ImageServer.Services.Repositories
{
    public class LocalRepository : IStorage
    {
        private readonly string _storagePath;

        /// <summary>
        /// Конструктор для ручной передачи строки хранилища
        /// </summary>
        public LocalRepository(string storagePath)
        {
            _storagePath = storagePath;
        }

        public LocalRepository(IOptions<StorageOptions> options)
        {
            _storagePath = options.Value.MainPath;
        }

        public async Task SaveFileAsync(Stream stream,
            string fileName,
            string relativePath, 
            CancellationToken ct = default)
        {
            var fullPath = Path.Combine(_storagePath, relativePath);

            var filePath = $"{Path.Combine(fullPath, fileName)}.webp" ;

            Directory.CreateDirectory(fullPath);

            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await stream.CopyToAsync(fileStream, ct);
        }

        public Task<Stream> GetFileAsync(string fileName,
            string relativePath, 
            CancellationToken ct = default)
        {
            var filePath = $"{Path.Combine(_storagePath, relativePath, fileName)}.webp";

            try
            {
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                return Task.FromResult<Stream>(stream);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    throw new FileNotFoundException($"Файл {fileName} не найден в {filePath}");
                else 
                    throw new InvalidOperationException("Произошла неизвестная ошибка при попытке получить файл.");
            }
        }

        public Task DeleteFileAsync(string fileName,
            string relativePath, 
            CancellationToken ct = default)
        {
            var filePath = $"{Path.Combine(_storagePath, relativePath, fileName)}.webp";

            if (!File.Exists(filePath)) throw new FileNotFoundException($"Файл {fileName} не найден в {filePath}");

            File.Delete(filePath);

            return Task.CompletedTask;
        }

        public Task ExecuteAsync(string fileName, 
            Func<IStorage, Task> operation, 
            CancellationToken ct = default) 
            => operation(this);
    }
}
