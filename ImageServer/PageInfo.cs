namespace ImageServer
{
    public class PageInfo<T>
    {
        public required List<T> Files { get; set; }
        public required int PageNumber { get; set; }
        public required int PageSize { get; set; }
        public required int TotalFilesCount { get; set; }
    }
}
