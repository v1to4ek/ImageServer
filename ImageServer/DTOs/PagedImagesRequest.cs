using ImageServer.Enums;

namespace ImageServer.DTOs
{
    public record class PagedImagesRequest
    {
        public int PageNumber { get; set; }
            
        public int PageSize { get; set; }

        public OrderingType OrderingType { get; set; }

        public ImageOrderingSelectors OrderingSelector { get; set; }
    };
}
