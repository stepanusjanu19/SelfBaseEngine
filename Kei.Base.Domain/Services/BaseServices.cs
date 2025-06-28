using Kei.Base.Domain.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Kei.Base.Extensions;
using Kei.Base.Models;

namespace Kei.Base.Domain.Services
{
    public abstract class BaseServices<TEntity> : IBaseServices<TEntity> where TEntity : class
    {
        protected readonly BaseRepository<TEntity> _repository;

        protected BaseServices(BaseRepository<TEntity> repository)
        {
            _repository = repository;
        }

        public virtual async Task<List<TEntity>> GetAllAsync(bool mapAllColumns = false)
        {
            var data = mapAllColumns
                ? _repository.GetAllColumn()
                : _repository.GetAll();

            return await Task.FromResult(data.ToList());
        }

        public virtual (List<TDestination> Data, int TotalCount) ExecutePaginate<TSource, TDestination>(
            int pageNumber,
            int pageSize,
            PaginationMode mode,
            IQueryable<TSource> baseQuery = null,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null,
            string sortColumn = null,
            bool isAscending = true)
            where TSource : class
        {
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> finalSort = sort;

            if (finalSort == null && !string.IsNullOrWhiteSpace(sortColumn))
            {
                finalSort = q => _repository.OrderByDynamic(q, sortColumn, isAscending);
            }
            switch (mode)
            {
                case PaginationMode.WithQuery:
                    return _repository.GetPaginateWithQuery(baseQuery, pageNumber, pageSize, filter, finalSort, selector);
                case PaginationMode.Project:
                    return _repository.GetPaginateProject(pageNumber, pageSize, filter, finalSort, selector);
                case PaginationMode.SelfWithQuery:
                case PaginationMode.Self:
                    if (typeof(TSource) != typeof(TDestination))
                        throw new InvalidOperationException("TSource and TDestination must be the same for Self/SelfWithQuery mode.");

                    var selfSelector = selector != null
                        ? (Func<TSource, TSource>)(object)selector
                        : null;

                    var result = mode == PaginationMode.SelfWithQuery
                        ? _repository.GetPaginateSelfWithQuery(baseQuery, pageNumber, pageSize, filter, finalSort, selfSelector)
                        : _repository.GetPaginateSelf(pageNumber, pageSize, filter, finalSort, selfSelector);

                    return ((List<TDestination>)(object)result.Data, result.TotalCount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), "Invalid pagination mode.");
            }
        }
        
        public virtual List<TDestination> GetMappedList<TDestination>(
            Expression<Func<TEntity, bool>> predicate = null)
        {
            return _repository.GetMappedList<TDestination>(predicate);
        }
        
        public virtual OperationResult<TEntity> GetById(params object[] keyValues)
            => _repository.GetById(keyValues);

        public virtual Task<OperationResult<TEntity>> GetByIdAsync(params object[] keyValues)
            => _repository.GetByIdAsync(keyValues);


        
        
    }
}
