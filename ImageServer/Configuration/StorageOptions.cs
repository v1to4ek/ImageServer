using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class StorageOptions : IConfigurationOption
    {
        public static string SectionName => "Storage";

        public string ImagesDirectoryName { get; set; } = "Images";

        public string PreviewsDirectoryName { get; set; } = "Previews";

        public string ImagesTrashDirectoryName { get; set; } = "ImagesTrash";

        public string PreviewsTrashDirectoryName { get; set; } = "PreviewsTrash";

        public string MainPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }
}
