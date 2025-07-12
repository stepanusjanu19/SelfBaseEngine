using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using Kei.Base.Domain.Functions;
using Kei.Base.Extensions;
using Kei.Base.Helper;
using Kei.Base.Models;
using Microsoft.EntityFrameworkCore.Query;

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

        TEntity GetFirstByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        );

        OperationResult<TEntity> GetByKeyOrFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        );

        OperationResult<TEntity> GetByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        );

        OperationResult<TResult> GetByFilterDataProjected<TResult>(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null,
            params object[] keyValues
        );

        OperationResult<List<TEntity>> GetByWhere(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null
        );

        OperationResult<List<TResult>> WhereProjected<TResult>(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null
        );

        IQueryable<TEntity> GetQueryableByFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null
        );

        IQueryable<TResult> GetProjectedByFilter<TResult>(
            List<FilterCondition<TEntity>> conditions,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection,
            List<string> includeProperties = null
        );

        Expression<Func<TEntity, bool>> UniqueFilter(TEntity entity);
        Task<OperationResult<TEntity>> AddAsync(TEntity entity);
        Task<OperationResult<TEntity>> UpdateAsync(TEntity entity);
        Task<OperationResult> DeleteAsync(params object[] keyValues);
        Task<OperationResult> DeleteAsync(TEntity entity);
        Task<OperationResult<List<TEntity>>> AddAsync(List<TEntity> entities);
        Task<OperationResult> DeleteAsync(List<TEntity> entities);
        Task<OperationResult> DeleteAsync(Expression<Func<TEntity, bool>> predicate);

        Task<OperationResult<int>> UpdateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> updateExpression);

        Task<OperationResult<List<TEntity>>> UpdateBulkAsync(
            List<TEntity> entities
        );

        OperationResult ExecuteRawSql(string sql, params object[] parameters);
        Task<OperationResult> ExecuteRawSqlAsync(string sql, params object[] parameters);
        List<TEntity> QueryRawSql(string sql, params object[] parameters);
        OperationResult ExecuteProcedure(string procName, params DbParameter[] parameters);
        Task<OperationResult> ExecuteProcedureAsync(string procName, params DbParameter[] parameters);
        DbParameter CreateParameter(string name, object? value, DbType? type = null);
        List<FilterCondition<TEntity>> BuildFilters(List<FilterCondition<TEntity>> userFilters = null);
        List<FilterCondition<TEntity>> BuildDynamicFilters(Action<FilterBuilder<TEntity>> build);
    }
}