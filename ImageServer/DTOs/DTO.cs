namespace ImageServer.DTOs
{
    public record PagedRequest(int PageNumber, int PageSize);

    public record PagedResponse<T>(List<T> Items);

    public record ImageDTO(string ImageUrl, string PreviewUrl);
}
