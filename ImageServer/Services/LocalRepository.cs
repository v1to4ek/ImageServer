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

            Directory.CreateDirectory(fullPath);

            await using var fileStream = new FileStream(filePath, FileMode.Create);

            await stream.CopyToAsync(fileStream);

            return filePath;
        }
    }
}
