namespace ImageServer.DTOs
{
    public record PagedRequest(int PageNumber, int PageSize, int TotalCount);

    public record PagedResponse<T>(List<T> Items);

    public record ThumbResponse();

    public record ImageResponse();
}
