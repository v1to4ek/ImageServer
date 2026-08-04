using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Enums;
using ImageServer.Models;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace ImageServer.DTOs
{
    public class PagedImagesServiceRequest 
        : PagedServiceRequestBase<ImageModel, ImageDTO, ImageOrderingSelectors>
    {
        private readonly string imagesDirectoryName;

        private readonly string previewsDirectoryName;

        private static readonly Dictionary<ImageOrderingSelectors, FilterDelegate> _filterDictionary =
            new Dictionary<ImageOrderingSelectors, FilterDelegate>()
            {
                [ImageOrderingSelectors.Name] = CreateFilter(image => image.Name),
                [ImageOrderingSelectors.Date] = CreateFilter(image => image.CreatedAt),
                [ImageOrderingSelectors.Favourite] = CreateFilter(image => image.IsFavourite)
            };

        public PagedImagesServiceRequest(int pageNumber, 
            int pageSize, 
            ImageOrderingSelectors selectorNum,
            OrderingType typeNum,
            StorageOptions options)
            : base(pageNumber, pageSize, selectorNum, typeNum)
        { 
            imagesDirectoryName = options.ImagesDirectoryName;

            previewsDirectoryName = options.PreviewsDirectoryName;
        }

        public override IReadOnlyDictionary<ImageOrderingSelectors, FilterDelegate> FilterDictionary
            => _filterDictionary;

        public override ImageOrderingSelectors DefaultSelector
            => ImageOrderingSelectors.Date;

        public override Expression<Func<ImageModel, ImageDTO>> DtoSelectorFactory
            => model
            => new ImageDTO(model.Id.ToString(),
                imagesDirectoryName,
                previewsDirectoryName,
                model.Name,
                model.IsFavourite,
                model.CreatedAt);

        public override FilterDelegate DefaultFilter 
            => _filterDictionary[ImageOrderingSelectors.Date];
    }
}
