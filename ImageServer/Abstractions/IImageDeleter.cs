namespace ImageServer.Abstractions
{
    public interface IImageDeleter
    {
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}
