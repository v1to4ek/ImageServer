namespace ImageServer.Services
{
    public interface IProcessingStrategy<TResult>
    {
        public Task<TResult> ProcessAsync(Stream stream, CancellationToken cancellationToken = default);
    }
}
