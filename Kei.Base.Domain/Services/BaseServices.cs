using Kei.Base.Domain.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Kei.Base.Domain.Functions;
using Kei.Base.Extensions;
using Kei.Base.Helper;
using Kei.Base.Models;
using Microsoft.EntityFrameworkCore.Query;

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

        public virtual TEntity GetFirstByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        ) => _repository.GetFirstByFilterData(conditions, includeProperties, keyValues);

        public virtual OperationResult<TEntity> GetByKeyOrFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        ) => _repository.GetByKeyOrFilter(conditions, includeProperties, keyValues);

        public virtual OperationResult<TEntity> GetByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
        ) => _repository.GetByFilterData(conditions, includeProperties, keyValues);

        public virtual OperationResult<TResult> GetByFilterDataProjected<TResult>(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null,
            params object[] keyValues
        ) => _repository.GetByFilterDataProjected(conditions, includeProperties, projection, keyValues);

        public virtual OperationResult<List<TEntity>> GetByWhere(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null
        ) => _repository.GetByWhere(conditions, includeProperties);

        public virtual OperationResult<List<TResult>> WhereProjected<TResult>(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null
        ) => _repository.WhereProjected(conditions, includeProperties, projection);

        public virtual IQueryable<TEntity> GetQueryableByFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null
        ) => _repository.GetQueryableByFilter(conditions, includeProperties);

        public virtual IQueryable<TResult> GetProjectedByFilter<TResult>(
            List<FilterCondition<TEntity>> conditions,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection,
            List<string> includeProperties = null
        ) => _repository.GetProjectedByFilter(conditions, projection, includeProperties);

        public virtual Expression<Func<TEntity, bool>> UniqueFilter(TEntity entity)
            => _repository.UniqueFilter(entity);

        public virtual Task<OperationResult<TEntity>> AddAsync(TEntity entity)
            => _repository.Add(entity);

        public virtual Task<OperationResult<TEntity>> UpdateAsync(TEntity entity)
            => _repository.Update(entity);

        public virtual Task<OperationResult> DeleteAsync(params object[] keyValues)
            => _repository.Delete(keyValues);

        public virtual Task<OperationResult> DeleteAsync(TEntity entity)
            => _repository.Delete(entity);

        #region  Bulk Entity Operations

        public virtual Task<OperationResult<List<TEntity>>> AddAsync(List<TEntity> entities)
            => _repository.Add(entities);

        public virtual Task<OperationResult> DeleteAsync(List<TEntity> entities)
            => _repository.Delete(entities);

        public virtual Task<OperationResult> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
            => _repository.Delete(predicate);

        public virtual Task<OperationResult<int>> UpdateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> updateExpression)
            => _repository.UpdateBulkAsync(predicate, updateExpression);

        public virtual Task<OperationResult<List<TEntity>>> UpdateBulkAsync(
            List<TEntity> entities
        ) => _repository.UpdateBulkAsync(entities);


        #endregion

        public virtual OperationResult ExecuteRawSql(string sql, params object[] parameters)
            => _repository.ExecuteRawSql(sql, parameters);

        public virtual Task<OperationResult> ExecuteRawSqlAsync(string sql, params object[] parameters)
            => _repository.ExecuteRawSqlAsync(sql, parameters);

        public virtual List<TEntity> QueryRawSql(string sql, params object[] parameters)
            => _repository.QueryRawSql(sql, parameters);

        public virtual OperationResult ExecuteProcedure(string procName, params DbParameter[] parameters)
            => _repository.ExecuteProcedure(procName, parameters);

        public virtual Task<OperationResult> ExecuteProcedureAsync(string procName, params DbParameter[] parameters)
            => _repository.ExecuteProcedureAsync(procName, parameters);

        public virtual DbParameter CreateParameter(string name, object? value, DbType? type = null)
            => _repository.CreateParameter(name, value, type);
        
        public virtual List<FilterCondition<TEntity>> BuildFilters(List<FilterCondition<TEntity>> userFilters = null)
            => _repository.BuildFilters(userFilters);

        public virtual List<FilterCondition<TEntity>> BuildDynamicFilters(Action<FilterBuilder<TEntity>> build)
            => _repository.BuildDynamicFilters(build);
    }
}
