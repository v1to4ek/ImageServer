namespace ImageServer.Services
{
    public interface IStorage
    {
        public Task<string> SaveAsync(Stream stream, string fileName, string relativePath);
    }
}
