namespace ImageServer.Services
{
    public interface IProcessingStrategy<TResult, TInput>
    {
        public Task<TResult> ProcessAsync(TInput input, CancellationToken cancellationToken = default);
    }
}
