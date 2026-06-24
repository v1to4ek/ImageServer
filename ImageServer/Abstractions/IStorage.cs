namespace ImageServer.Abstractions
{
    public interface IStorage
    {
        public Task SaveAsync(Stream stream, string fileName, string relativePath);

        public Stream GetFile(string fileName, string relativePath);

        public void DeleteFile(string fileName, string relativePath);
    }
}
