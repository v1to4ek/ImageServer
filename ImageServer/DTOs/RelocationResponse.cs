namespace ImageServer.DTOs
{
    public record class RelocationResponse(List<string> successful, List<string> failed)
    {
        public int SuccessCount => successful.Count;

        public int FailedCount => failed.Count;
    }
}
