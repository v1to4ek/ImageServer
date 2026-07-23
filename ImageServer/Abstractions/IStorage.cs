namespace ImageServer.Abstractions
{
    public interface IStorage
    {
        public Task SaveFileAsync(Stream stream, 
            string fileName,
            string relativePath, 
            CancellationToken ct = default);

        public Task<Stream> GetFileAsync(string fileName,
            string relativePath,
            CancellationToken ct = default);

        public Task DeleteFileAsync(string fileName,
            string relativePath, 
            CancellationToken ct = default);

        public Task ExecuteAsync(string fileName,
            Func<IStorage, Task> operation,
            CancellationToken ct = default);
    }
}
