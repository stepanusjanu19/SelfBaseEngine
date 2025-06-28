using Kei.Base.Helper;
using Kei.Base.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Repository
{
    public interface IBaseRepository<TEntity> where TEntity : class
    {
        IQueryable<TEntity> GetAll();
        IEnumerable<TEntity> GetAllColumns();
        (List<TDestination> Data, int TotalCount) GetPaginateWithQuery<TSource, TDestination>(
            IQueryable<TSource> baseQuery,
            int pageNumber,
            int pageSize,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null)
        where TSource : class;
        (List<TDestination> Data, int TotalCount) GetPaginateProject<TSource, TDestination>(
            int pageNumber,
            int pageSize,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null)
        where TSource : class;
        (List<T> Data, int TotalCount) GetPaginateSelfWithQuery<T>(
            IQueryable<T> baseQuery,
            int pageNumber,
            int pageSize,
            Func<IQueryable<T>, IQueryable<T>> filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> sort = null,
            Func<T, T> selector = null)
        where T : class;
        (List<T> Data, int TotalCount) GetPaginateSelf<T>(
            int pageNumber,
            int pageSize,
            Func<IQueryable<T>, IQueryable<T>> filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> sort = null,
            Func<T, T> selector = null)
        where T : class;
        IOrderedQueryable<T> OrderByDynamic<T>(IQueryable<T> source, string columnName, bool ascending);
        List<TDestination> GetMappedList<TDestination>(Expression<Func<TEntity, bool>> predicate = null);
        OperationResult<TEntity> GetById(params object[] keyValues);
        Task<OperationResult<TEntity>> GetByIdAsync(params object[] keyValues);
        TEntity GetFirstByFilterData( List<FilterCondition<TEntity>> conditions = null, List<string> includeProperties = null, params object[] keyValues);

        //Task<OperationResult<List<TEntity>>> GetAllResultAsync();
        //Task<OperationResult<List<TEntity>>> GetAllResultAsync();
    }
}
