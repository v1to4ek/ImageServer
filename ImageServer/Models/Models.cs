namespace ImageServer.Models
{
    public class ImageModel
    {
        public Guid Id { get; set; }

        public string ImageUrl { get; set; }

        public string PreviewUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public ImageModel() { }

        public ImageModel(Guid id, string imageUrl, string previewUrl)
        {
            Id = id;
            ImageUrl = imageUrl;
            PreviewUrl = previewUrl;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
