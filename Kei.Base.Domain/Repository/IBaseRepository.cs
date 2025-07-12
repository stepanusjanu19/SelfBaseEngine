using Kei.Base.Helper;
using Kei.Base.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Kei.Base.Domain.Functions;
using Microsoft.EntityFrameworkCore.Query;

namespace Kei.Base.Domain.Repository
{
    public interface IBaseRepository<TEntity> where TEntity : class
    {
        IQueryable<TEntity> GetAll();
        IEnumerable<TEntity> GetAllColumn();
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
        OperationResult<TEntity> GetByFilterData(List<FilterCondition<TEntity>> conditions = null, List<string> includeProperties = null,params object[] keyValues);
        OperationResult<TResult> GetByFilterDataProjected<TResult>(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null,
            params object[] keyValues);
        OperationResult<List<TEntity>> GetByWhere(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null);
        OperationResult<List<TResult>> WhereProjected<TResult>(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null);
        OperationResult<TEntity> GetByKeyOrFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues);
        IQueryable<TResult> GetProjectedByFilter<TResult>(
            List<FilterCondition<TEntity>> conditions,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection,
            List<string> includeProperties = null);
        IQueryable<TEntity> GetQueryableByFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null);

        Expression<Func<TEntity, bool>> UniqueFilter(TEntity entity);
        Task<OperationResult<TEntity>> Add(TEntity entity);
        Task<OperationResult<TEntity>> Update(TEntity entity);
        Task<OperationResult> Delete(params object[] keyValues);
        Task<OperationResult> Delete(TEntity entity);
        
        #region Transaction Begin & Batch
        Task<OperationResult<List<TEntity>>> Add(List<TEntity> entities);
        Task<OperationResult> Delete(List<TEntity> entities);
        Task<OperationResult> Delete(Expression<Func<TEntity, bool>> predicate);
        Task<OperationResult<int>> UpdateBulkAsync(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> updateExpression);
        Task<OperationResult<List<TEntity>>> UpdateBulkAsync(List<TEntity> entities);

        #endregion

        OperationResult ExecuteRawSql(string sql, params object[] parameters);
        Task<OperationResult> ExecuteRawSqlAsync(string sql, params object[] parameters);
        List<TEntity> QueryRawSql(string sql, params object[] parameters);
        OperationResult ExecuteProcedure(string procFullName, params DbParameter[] parameters);
        Task<OperationResult> ExecuteProcedureAsync(string procFullName, params DbParameter[] parameters);
        DbParameter CreateParameter(string name, object? value, DbType? type = null);
    }
}
