using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public static class ConfigExtensions
    {
        public static void AddConfigOption<TOption>(this WebApplicationBuilder applicationBuilder)
            where TOption : class, IConfigurationOption
        {
            applicationBuilder.Services.Configure<TOption>(applicationBuilder.Configuration.GetSection(TOption.SectionName));
        }
    }
}
