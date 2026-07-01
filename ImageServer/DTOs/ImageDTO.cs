namespace ImageServer.DTOs
{
    public record ImageDTO
    {
        public string Id { get; set; }

        public string ImageUrl { get; set; }

        public string PreviewUrl { get; set; }

        public string Name { get; set; }

        public bool Favorite { get; set; }

        public DateTime Date { get; set; }

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
