namespace ImageServer.Services
{
    public class LocalRepository : IStorage
    {
        private readonly string _storagePath;

        public LocalRepository(string storagePath)
        {
            _storagePath = storagePath;
        }

        public async Task<string> SaveAsync(Stream stream, string fileName, string relativePath)
        {
            var fullPath = Path.Combine(_storagePath, relativePath);

            var filePath = Path.Combine(fullPath, fileName);

            var urlPath = Path.Combine(relativePath, fileName).Replace("\\", "/");

            Directory.CreateDirectory(fullPath);

            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await stream.CopyToAsync(fileStream);

            return urlPath;
        }

        public Stream GetFile(string fileName, string relativePath)
        {
            var filePath = Path.Combine(_storagePath, relativePath, fileName);

            if (!File.Exists(filePath)) throw new FileNotFoundException();

            var stream = new FileStream(filePath,FileMode.Open,FileAccess.Read);

            return stream;
        }

        public void DeleteFile(string fileName, string relativePath)
        {
            var filePath = Path.Combine(_storagePath, relativePath, fileName);

            if (!File.Exists(filePath)) throw new FileNotFoundException();

            File.Delete(filePath);
        }
    }
}
