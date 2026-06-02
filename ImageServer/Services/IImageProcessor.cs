namespace ImageServer.Services
{
    public interface IImageProcessor
    {
        public Task<TResult> ProcessAsync<TStrategy, TResult, TInput>(TInput input, CancellationToken ct = default)
            where TStrategy : class, IProcessingStrategy<TResult, TInput>;
    }
}
