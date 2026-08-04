namespace ImageServer.DTOs
{
    public record class ImageDTO
    {
        public string Id { get; init; }

        public string ImageUrl { get; init; }

        public string PreviewUrl { get; init; }

        public string Name { get; init; }

        public bool Favorite { get; init; }

        public DateTime Date { get; init; }

        public ImageDTO(string id, string imageDirectory, string previewDirectory, string name, bool favourite, DateTime date)
        {
            Id = id;

            ImageUrl = Path.Combine(imageDirectory, id).Replace("\\", "/");

            PreviewUrl = Path.Combine(previewDirectory, id).Replace("\\", "/");

            Name = name;

            Date = date;

            Favorite = favourite;
        }

    }

}
