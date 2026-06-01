namespace ImageServer.Services
{
    public interface IImageProcessor
    {
        public Task<TResult> ProcessAsync<TResult, TStrategy>(Stream stream, CancellationToken ct = default)
            where TStrategy : class, IProcessingStrategy<TResult>;
    }
}
