using ImageServer.Abstractions;
using ImageServer.Configuration;
using Microsoft.Extensions.Options;

namespace ImageServer.Services
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

        public async Task SaveAsync(Stream stream, string fileId, string relativePath)
        {
            var fullPath = Path.Combine(_storagePath, relativePath);

            var filePath = Path.Combine(fullPath, fileId);

            Directory.CreateDirectory(fullPath);

            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await stream.CopyToAsync(fileStream);
        }

        public Stream GetFile(string fileId, string relativePath)
        {
            var filePath = $"{Path.Combine(_storagePath, relativePath, fileId)}.webp";

            if (!File.Exists(filePath)) throw new FileNotFoundException();

            var stream = new FileStream(filePath,FileMode.Open,FileAccess.Read);

            return stream;
        }

        public void DeleteFile(string fileId, string relativePath)
        {
            var filePath = $"{Path.Combine(_storagePath, relativePath, fileId)}.webp";

            if (!File.Exists(filePath)) throw new FileNotFoundException();

            File.Delete(filePath);
        }
    }
}
