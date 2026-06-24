namespace ImageServer.DTOs
{
    public record ImageDTO
    {
        public string ImageUrl { get; set; }

        public string PreviewUrl { get; set; }

        public ImageDTO(string Id, string ImageDirectory, string PreviewDirectory)
        {
            ImageUrl = Path.Combine(ImageDirectory, Id).Replace("\\", "/");

            PreviewUrl = Path.Combine(PreviewDirectory, Id).Replace("\\", "/");
        }

    }
}
