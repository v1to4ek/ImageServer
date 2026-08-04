using ImageServer.Enums;
using System.Linq.Expressions;

namespace ImageServer.Abstractions
{
    public abstract class PagedServiceRequestBase<TDBModel,TDto,TOrderingSelector>
        where TDBModel : class
        where TDto : class
        where TOrderingSelector : struct, Enum
    {
        public int PageNumber { get; init; }

        public int PageSize { get; init; }

        public TOrderingSelector OrderingSelector { get; init; }

        public OrderingType OrderingType { get; init; }

        protected PagedServiceRequestBase(int pageNumber, int pageSize, TOrderingSelector selectorNum, OrderingType typeNum)
        {
            PageNumber = pageNumber;

            PageSize = pageSize;

            OrderingSelector = selectorNum;

            OrderingType = typeNum;
        }

        public delegate IOrderedQueryable<TDBModel> FilterDelegate(IQueryable<TDBModel> sourceQuery, bool ascending);

        public abstract IReadOnlyDictionary<TOrderingSelector, FilterDelegate> FilterDictionary { get; }

        public abstract TOrderingSelector DefaultSelector { get; }

        public abstract FilterDelegate DefaultFilter { get; }

        public abstract Expression<Func<TDBModel, TDto>> DtoSelectorFactory { get; }

        protected static FilterDelegate CreateFilter<TSelectorKey>
            (Expression<Func<TDBModel, TSelectorKey>> selector) =>
            (query, ascending) => ascending
            ? query.OrderBy(selector)
            : query.OrderByDescending(selector);
    }
}
