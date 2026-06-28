using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class StorageOptions : IConfigurationOption
    {
        public static string SectionName => "Storage";

        public string ImagesDirectoryName { get; set; } = "images";

        public string PreviewsDirectoryName { get; set; } = "previews";

        public string MainPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }
}
