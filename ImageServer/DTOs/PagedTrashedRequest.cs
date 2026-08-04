using ImageServer.Enums;

namespace ImageServer.DTOs
{
    public record class PagedTrashedRequest
    {
        public int PageNumber { get; set; } 

        public int PageSize { get; set; }

        public OrderingType OrderingType { get; set; }

        public TrashOrderingSelectors OrderingSelector { get; set; }
    }
}
