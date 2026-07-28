namespace ImageServer.Models
{
    public class FileToDeletionModel
    {
        public Guid Id { get; set; }

        public DateTime TrashedAt { get; set; }

        public FileToDeletionModel() { }

        public FileToDeletionModel(Guid id)
        {
            Id = id;
            TrashedAt = DateTime.UtcNow;
        }
    }
}
