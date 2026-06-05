namespace ImageServer.Services
{

    /// <summary>
    /// Стратегия валидации расширения файла.
    /// Работает синхронно.
    /// Проверяет что расширение входного файла входит в список допустимых форматов.
    /// Предполагает использовние bool в качестве TResult и string в качестве TInput внутри метода ProcessAsync интерфейса IImageProcessor.
    /// </summary>
    public class ExtentionValidationProcessor : IProcessingStrategy<bool, string>
    {
        private readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        public Task<bool> ProcessAsync(string input, CancellationToken cancellationToken = default)
        {
            var extention = Path
                .GetExtension(input)
                .ToLower();

            var result = true;

            if(!_allowedExtensions.Contains(extention)) { result = false; }

            return Task.FromResult(result);
        }
    }
}
