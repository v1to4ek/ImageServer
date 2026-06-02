namespace ImageServer.Services
{
    public class ImageProcessor : IImageProcessor
    {
        private readonly IServiceProvider _serviceProvider;

        public ImageProcessor(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        /// <summary>
        /// Выполняет обработку входных данных с помощью указанной стратегии.
        /// TResult и TInput должны соответствовать типам обобщённой стратегии
        /// </summary>
        /// <typeparam name="TStrategy">
        /// Тип стратегии обработки. Должен быть зарегистрирован в DI
        /// и реализовывать <see cref="IProcessingStrategy{TResult, TInput}"/>.
        /// </typeparam>
        /// <typeparam name="TResult">Тип возвращаемого результата обработки.</typeparam>
        /// <typeparam name="TInput">Тип входных данных для обработки.</typeparam>
        /// <param name="input">Входные данные передаваемые в стратегию.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Результат обработки входных данных стратегией.</returns>
        public async Task<TResult> ProcessAsync<TStrategy, TResult, TInput>
            (TInput input,
            CancellationToken ct = default)
            where TStrategy : class, IProcessingStrategy<TResult, TInput>
        {
            var processor = _serviceProvider.GetRequiredService<TStrategy>();

            var result = await processor.ProcessAsync(input, ct);

            return result;
        }
    }
}
