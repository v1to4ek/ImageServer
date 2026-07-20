namespace ImageServer.Abstractions
{
    public interface IStorage
    {
        public Task SaveFileAsync(Stream stream, string fileName, string relativePath, CancellationToken ct = default);

        public Stream GetFile(string fileName, string relativePath);

        public void DeleteFile(string fileName, string relativePath);
    }
}
