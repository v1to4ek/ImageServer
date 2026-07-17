namespace ImageServer.Models
{
    public class FileToDeletionModel
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public int DeletionAttempts { get; set; }

        public FileToDeletionModel() { }

        public FileToDeletionModel(Guid id)
        {
            Id = id;
            CreatedAt = DateTime.UtcNow;
            DeletionAttempts = 0;
        }
    }
}
