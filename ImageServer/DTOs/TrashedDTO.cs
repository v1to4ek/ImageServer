namespace ImageServer.DTOs
{
    public record class TrashedDTO
    {
        public string Id { get; init; }

        public DateTime TrashedAt { get; init; }

        public TrashedDTO(string id , DateTime trshedAt)
        {
            Id = id;

            TrashedAt = trshedAt;
        }


    }
}
