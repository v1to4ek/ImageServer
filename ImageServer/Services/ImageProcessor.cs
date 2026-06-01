namespace ImageServer.Services
{
    public class ImageProcessor : IImageProcessor
    {
        private readonly IServiceProvider _serviceProvider;

        public ImageProcessor(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public async Task<TResult> ProcessAsync<TResult, TStrategy>
            (Stream inputStream,
            CancellationToken ct = default)
            where TStrategy : class, IProcessingStrategy<TResult>
        {
            var processor = _serviceProvider.GetRequiredService<TStrategy>();

            var result = await processor.ProcessAsync(inputStream, ct);

            return result;
        }
    }
}
