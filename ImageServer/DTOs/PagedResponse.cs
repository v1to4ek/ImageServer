namespace ImageServer.DTOs
{
    public record PagedResponse<T>(
        IEnumerable<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize)
    {
        public int totalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => PageNumber < totalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }
}
