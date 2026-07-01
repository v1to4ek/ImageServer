using ImageServer.Enums;

namespace ImageServer.DTOs
{
    public record PagedRequest
    {
        public int PageNumber {  get; set; }
            
        public int PageSize { get; set; }

        public OrderingTypes OrderingType { get; set; }

        public OrderingSelectors OrderingSelector { get; set; }
    };
}
