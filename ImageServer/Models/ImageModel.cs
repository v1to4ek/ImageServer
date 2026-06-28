namespace ImageServer.Models
{
    public class ImageModel
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Name { get; set; }

        public bool IsFavourite { get; set; }

        public ImageModel() { }

        public ImageModel(Guid id, bool isFavourite = false)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
            Name = $"img {DateTime.Now}";
            IsFavourite = isFavourite;
        }

        public ImageModel(Guid id, string name, bool isFavourite = false)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
            Name = name;
            IsFavourite = isFavourite;
        }
    }
}
