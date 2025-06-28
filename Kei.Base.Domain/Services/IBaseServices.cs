using System.Linq.Expressions;
using Kei.Base.Extensions;
using Kei.Base.Models;

namespace Kei.Base.Domain.Services
{
    public interface IBaseServices<TEntity> where TEntity : class
    {
        Task<List<TEntity>> GetAllAsync(bool mapAllColumns = false);

        (List<TDestination> Data, int TotalCount) ExecutePaginate<TSource, TDestination>(
            int pageNumber,
            int pageSize,
            PaginationMode mode,
            IQueryable<TSource> baseQuery = null,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null,
            string sortColumn = null,
            bool isAscending = true)
            where TSource : class;

        List<TDestination> GetMappedList<TDestination>(
            Expression<Func<TEntity, bool>> predicate = null);

        OperationResult<TEntity> GetById(params object[] keyValues);
        Task<OperationResult<TEntity>> GetByIdAsync(params object[] keyValues);
    }
}