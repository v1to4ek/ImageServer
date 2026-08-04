namespace ImageServer.DTOs
{
    public record class SavedResult(int SuccessCount, List<string>? ErrorList);
}
