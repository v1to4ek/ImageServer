using ImageServer.Abstractions;
using ImageServer.Enums;
using ImageServer.Models;
using System.Linq.Expressions;

namespace ImageServer.DTOs
{
    public class PagedTrashedServiceRequest
        : PagedServiceRequestBase<FileToDeletionModel, TrashedDTO, TrashOrderingSelectors>
    {
        private static readonly Dictionary<TrashOrderingSelectors, FilterDelegate> _filterDictionary =
            new Dictionary<TrashOrderingSelectors, FilterDelegate>()
            {
                [TrashOrderingSelectors.Id] = CreateFilter(item => item.Id),
                [TrashOrderingSelectors.CreatedAt] = CreateFilter(item => item.TrashedAt)
            };

        public PagedTrashedServiceRequest(int pageNumber, 
            int pageSize,
            TrashOrderingSelectors selectorNum, 
            OrderingType typeNum) : base(pageNumber, pageSize, selectorNum, typeNum) { }

        public override IReadOnlyDictionary<TrashOrderingSelectors, FilterDelegate> FilterDictionary
            => _filterDictionary;

        public override TrashOrderingSelectors DefaultSelector
            => TrashOrderingSelectors.CreatedAt;

        public override Expression<Func<FileToDeletionModel, TrashedDTO>> DtoSelectorFactory
            => model
            => new TrashedDTO(model.Id.ToString(),
                model.TrashedAt);

        public override FilterDelegate DefaultFilter 
            => _filterDictionary[TrashOrderingSelectors.CreatedAt];
    }
}
