using AutoMapper.QueryableExtensions;
using AutoMapper;
using Kei.Base.Helper;
using Kei.Base.Models;
using Kei.Base.Domain.Mapping;
using Kei.Base.Extensions;
using System.Collections.Concurrent;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Data;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Kei.Base.Domain.Functions
{
    public abstract class BaseFunctions<TEntity> where TEntity : class
    {
        protected readonly DbContext _context;
        protected readonly DbSet<TEntity> _dbSet;
        protected readonly IConfigurationProvider _mapperConfig;
        protected readonly IMapper _mapper;
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<IProperty>> _keyPropertyCache = new();


        protected BaseFunctions(DbContext context)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();
            _mapperConfig = MappingConfigProvider.GetPlainConfig<TEntity, TEntity>();
            _mapper = MappingConfigProvider.ToSafeMapper<TEntity, TEntity>();
        }

        public virtual DbContext Context => _context;

        public virtual IQueryable<TEntity> GetAll()
        {
            return _dbSet.AsNoTracking().ProjectTo<TEntity>(_mapperConfig);
        }

        public virtual IEnumerable<TEntity> GetAllColumn()
        {
            return _dbSet
                .AsNoTracking()
                .Select(e => _mapper.Map<TEntity>(e))
                .ToList();
        }

        public virtual async Task<int> GetCountData(List<FilterCondition<TEntity>> conditions = null)
            => await GetQueryableByFilter(conditions).CountAsync();
        
        public virtual (List<TDestination> Data, int TotalCount) GetPaginateWithQuery<TSource, TDestination>(
            IQueryable<TSource> baseQuery,
            int pageNumber,
            int pageSize,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null)
            where TSource : class
        {
            var query = baseQuery ?? _context.Set<TSource>().AsNoTracking();

            if (filter != null)
                query = filter(query);

            int totalCount = query.Count();

            query = sort != null
                    ? sort(query)
                    : query.OrderBy(e => EF.Property<object>(e, typeof(TSource).GetProperties().First().Name));


            var pagedData = query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = selector != null
                ? pagedData.Select(selector).ToList()
                : pagedData.Cast<TDestination>().ToList();

            return (result, totalCount);
        }
        public virtual (List<TDestination> Data, int TotalCount) GetPaginateProject<TSource, TDestination>(
            int pageNumber,
            int pageSize,
            Func<IQueryable<TSource>, IQueryable<TSource>> filter = null,
            Func<IQueryable<TSource>, IOrderedQueryable<TSource>> sort = null,
            Func<TSource, TDestination> selector = null)
            where TSource : class
        {
            var baseQuery = _context.Set<TSource>().AsNoTracking();
            return GetPaginateWithQuery(baseQuery, pageNumber, pageSize, filter, sort, selector);
        }

        public virtual (List<T> Data, int TotalCount) GetPaginateSelfWithQuery<T>(
            IQueryable<T> baseQuery,
            int pageNumber,
            int pageSize,
            Func<IQueryable<T>, IQueryable<T>> filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> sort = null,
            Func<T, T> selector = null)
            where T : class
        {
            return GetPaginateWithQuery<T, T>(baseQuery, pageNumber, pageSize, filter, sort, selector);
        }
        public virtual (List<T> Data, int TotalCount) GetPaginateSelf<T>(
            int pageNumber,
            int pageSize,
            Func<IQueryable<T>, IQueryable<T>> filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> sort = null,
            Func<T, T> selector = null)
            where T : class
        {
            var baseQuery = _context.Set<T>().AsNoTracking();
            return GetPaginateWithQuery<T, T>(baseQuery, pageNumber, pageSize, filter, sort, selector);
        }

        public virtual IOrderedQueryable<T> OrderByDynamic<T>(IQueryable<T> source, string columnName, bool ascending)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return (IOrderedQueryable<T>)source;

            var entityType = _context.Model.FindEntityType(typeof(T));
            var propertyFromEF = entityType?
            .GetProperties()
                .FirstOrDefault(p => string.Equals(p.GetColumnName(), columnName, StringComparison.OrdinalIgnoreCase));

            var propertyName = propertyFromEF?.Name;

            if (propertyName == null)
            {
                var clrProp = typeof(T).GetProperties()
                .FirstOrDefault(p =>
                        string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), true)
                                .OfType<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()
                                .FirstOrDefault()?.Name ?? "",
                            columnName,
                            StringComparison.OrdinalIgnoreCase));

                propertyName = clrProp?.Name;
            }

            if (string.IsNullOrEmpty(propertyName))
                return (IOrderedQueryable<T>)source;

            IOrderedQueryable<T> query;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, propertyName);

            var converted = Expression.Convert(property, typeof(object));
            var propertyLambda = Expression.Lambda<Func<T, object>>(converted, parameter);

            //var property = Expression.PropertyOrField(parameter, propertyName);

            if (property.Type.IsValueType && Nullable.GetUnderlyingType(property.Type) == null)
            {
                query = ascending
                    ? source.OrderBy(propertyLambda)
                    : source.OrderByDescending(propertyLambda);
            }
            else
            {
                var notNullExpr = Expression.NotEqual(property, Expression.Constant(null, property.Type));
                var notNullLambda = Expression.Lambda<Func<T, bool>>(notNullExpr, parameter);

                query = ascending
                    ? source.OrderBy(notNullLambda).ThenBy(propertyLambda)
                    : source.OrderByDescending(notNullLambda).ThenByDescending(propertyLambda);
            }

            return query;
        }


        public virtual List<TDestination> GetMappedList<TDestination>(
            Expression<Func<TEntity, bool>> predicate = null)
        {
            IQueryable<TEntity> query = _dbSet.AsNoTracking();

            if (predicate != null)
                query = query.Where(predicate);

            if (typeof(TEntity) == typeof(TDestination))
            {
                return query.Cast<TDestination>().ToList();
            }

            if (typeof(TDestination).Namespace != typeof(TEntity).Namespace)
            {
                return query
                    .ProjectTo<TDestination>(_mapperConfig)
                    .ToList();
            }

            var mapper = MappingConfigProvider.ToSafeMapper<TEntity, TDestination>();
            return query
                .ToList()
                .Select(e => mapper.Map<TDestination>(e))
                .ToList();
        }

        public virtual List<TOut> GetMappedListToList<TMid, TOut>(
            Expression<Func<TEntity, bool>> predicate = null
        )
        {
            var first = GetMappedList<TMid>(predicate);
            var mapper = MappingConfigProvider.ToSafeMapper<TMid, TOut>();
            return first.Select(e => mapper.Map<TOut>(e)).ToList();
        }

        public virtual List<TOut> MapList<TIn, TOut>(List<TIn> list)
        {
            var mapper = MappingConfigProvider.ToSafeMapper<TIn, TOut>();
            return list.Select(e => mapper.Map<TOut>(e)).ToList();
        }

        public virtual IEnumerable<TOut> MapSelect<TIn, TOut>(List<TIn> list)
        {
            var mapper = MappingConfigProvider.ToSafeMapper<TIn, TOut>();
            return list.Select(e => mapper.Map<TOut>(e));
        }

        public virtual OperationResult<TEntity> GetById(params object[] keyValues)
        {
            var entity = _dbSet.Find(keyValues);
            return entity != null
                ? OperationResult<TEntity>.Ok(entity)
                : OperationResult<TEntity>.Fail("Entity not found.");
        }

        public virtual async Task<OperationResult<TEntity>> GetByIdAsync(params object[] keyValues)
        {
            var entity = await _dbSet.FindAsync(keyValues);
            return entity != null
                ? OperationResult<TEntity>.Ok(entity)
                : OperationResult<TEntity>.Fail("Entity not found.");
        }
        
        public virtual TResult GetProjectedJoinByFilter<TJoin, TKey, TResult>(
            List<FilterCondition<TEntity>> conditions,
            Expression<Func<TEntity, TKey>> outerKeySelector,
            Expression<Func<TJoin, TKey>> innerKeySelector,
            Expression<Func<TEntity, TJoin, TResult>> selector)
            where TJoin : class
        {
            IQueryable<TEntity> queryA = _dbSet.AsQueryable();
            IQueryable<TJoin> queryB = _context.Set<TJoin>().AsQueryable();

            var filterUser = BuildFilters(conditions);
            if (filterUser == null || !filterUser.Any())
                return default;

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in filterUser)
            {
                if (condition.Value is DateTime dateValue)
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            queryA = queryA.Where(lambda);
            var joinedQuery = queryA.Join(queryB, outerKeySelector, innerKeySelector, selector);
            try
            {
                return joinedQuery.FirstOrDefault();
            }
            catch (InvalidOperationException)
            {
                return joinedQuery.AsEnumerable().FirstOrDefault();
            }
        }
        
        public virtual async Task<TResult> GetProjectedJoinByFilterAsync<TJoin, TKey, TResult>(
            List<FilterCondition<TEntity>> conditions,
            Expression<Func<TEntity, TKey>> outerKeySelector,
            Expression<Func<TJoin, TKey>> innerKeySelector,
            Expression<Func<TEntity, TJoin, TResult>> selector)
            where TJoin : class
        {
            IQueryable<TEntity> queryA = _dbSet.AsQueryable();
            IQueryable<TJoin> queryB = _context.Set<TJoin>().AsQueryable();

            var filterUser = BuildFilters(conditions);
            if (filterUser == null || !filterUser.Any())
                return default;

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in filterUser)
            {
                if (condition.Value is DateTime dateValue)
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            queryA = queryA.Where(lambda);

            var joinedQuery = queryA.Join(queryB, outerKeySelector, innerKeySelector, selector);

            try
            {
                return await joinedQuery.FirstOrDefaultAsync();
            }
            catch (InvalidOperationException)
            {
                return joinedQuery.AsEnumerable().FirstOrDefault();
            }
        }
        
        public virtual async Task<List<TResult>> GetProjectedJoinListByFilterAsync<TJoin, TKey, TResult>(
            List<FilterCondition<TEntity>> conditions,
            Expression<Func<TEntity, TKey>> outerKeySelector,
            Expression<Func<TJoin, TKey>> innerKeySelector,
            Expression<Func<TEntity, TJoin, TResult>> selector)
            where TJoin : class
        {
            IQueryable<TEntity> queryA = _dbSet.AsQueryable();
            IQueryable<TJoin> queryB = _context.Set<TJoin>().AsQueryable();

            var filterUser = BuildFilters(conditions);
            if (filterUser == null || !filterUser.Any())
                return new List<TResult>();

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in filterUser)
            {
                if (condition.Value is DateTime dateValue)
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            queryA = queryA.Where(lambda);

            var joinedQuery = queryA.Join(queryB, outerKeySelector, innerKeySelector, selector);

            try
            {
                return await joinedQuery.ToListAsync();
            }
            catch (InvalidOperationException)
            {
                return joinedQuery.AsEnumerable().ToList();
            }
        }

        public virtual TEntity GetFirstByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues)
        {
            IQueryable<TEntity> query = _dbSet;

            if (includeProperties != null)
            {
                foreach (var include in includeProperties)
                    query = query.Include(include);
            }

            if (keyValues != null && keyValues.Length > 0)
            {
                var entityByKey = _dbSet.Find(keyValues);
                if (entityByKey != null)
                    return entityByKey;
            }

            if (conditions == null || !conditions.Any())
                return null;

            var filterUser = BuildFilters(conditions);
            if (filterUser == null || !filterUser.Any())
                return null;

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in filterUser)
            {
                if (condition.Value is DateTime dateValue)
                {
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);
                }

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            try
            {
                return query.FirstOrDefault(lambda);
            }
            catch (InvalidOperationException)
            {
                return query.AsEnumerable().FirstOrDefault(lambda.Compile());
            }
        }

        public virtual OperationResult<TEntity> GetByFilterData(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues
            )
        {
            if (keyValues != null && keyValues.Length > 0)
            {
                var entityByKey = _dbSet.Find(keyValues);
                if (entityByKey != null)
                    return OperationResult<TEntity>.Ok(entityByKey);
            }

            if (conditions == null || !conditions.Any())
                return OperationResult<TEntity>.Fail("No conditions or key values provided.");

            IQueryable<TEntity> query = _dbSet;

            if (includeProperties != null)
            {
                foreach (var include in includeProperties)
                    query = query.Include(include);
            }

            var filterUser = BuildFilters(conditions);

            if (filterUser == null || !filterUser.Any())
                return OperationResult<TEntity>.Fail("No filtered or key values provided.");

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in filterUser)
            {
                if (condition.Value is DateTime dateValue)
                {
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);
                }

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            var resultEntity = query.FirstOrDefault(lambda);

            return resultEntity != null
                ? OperationResult<TEntity>.Ok(resultEntity)
                : OperationResult<TEntity>.Fail("Entity not found.");
        }


        public virtual OperationResult<TResult> GetByFilterDataProjected<TResult>(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null,
        params object[] keyValues)
        {
            if (keyValues != null && keyValues.Length > 0)
            {
                var entity = _dbSet.Find(keyValues);
                if (entity != null)
                {
                    TResult mapped = projection != null
                        ? projection(new[] { entity }.AsQueryable()).FirstOrDefault()
                        : _mapper.Map<TResult>(entity);

                    return OperationResult<TResult>.Ok(mapped);
                }
            }

            var filteredQuery = GetQueryableByFilter(conditions, includeProperties);

            TResult result = projection != null
                ? projection(filteredQuery).FirstOrDefault()
                : filteredQuery.ProjectTo<TResult>(_mapper.ConfigurationProvider).FirstOrDefault();

            return result != null
                ? OperationResult<TResult>.Ok(result)
                : OperationResult<TResult>.Fail("Entity not found.");
        }


        public virtual OperationResult<List<TEntity>> GetByWhere(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null)
        {
            var query = GetQueryableByFilter(conditions, includeProperties);
            var resultList = query.ToList();

            return resultList.Any()
                ? OperationResult<List<TEntity>>.Ok(resultList)
                : OperationResult<List<TEntity>>.Fail("No matching entities found.");
        }


        public virtual OperationResult<List<TResult>> WhereProjected<TResult>(
            List<FilterCondition<TEntity>> conditions,
            List<string> includeProperties = null,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection = null)
        {
            var query = GetQueryableByFilter(conditions, includeProperties);

            var projectedQuery = projection != null ? projection(query) : query.ProjectTo<TResult>(_mapper.ConfigurationProvider);
            var resultList = projectedQuery.ToList();

            return resultList.Any()
                ? OperationResult<List<TResult>>.Ok(resultList)
                : OperationResult<List<TResult>>.Fail("No matching records found.");
        }

        public virtual OperationResult<TEntity> GetByKeyOrFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null,
            params object[] keyValues)
        {
            if (keyValues != null && keyValues.Length > 0)
            {
                var entity = _dbSet.Find(keyValues);
                if (entity != null)
                    return OperationResult<TEntity>.Ok(entity);
            }

            if (conditions == null || !conditions.Any())
                return OperationResult<TEntity>.Fail("No key or filter conditions provided.");

            var query = GetQueryableByFilter(conditions, includeProperties);
            var entityFiltered = query.FirstOrDefault();

            return entityFiltered != null
                ? OperationResult<TEntity>.Ok(entityFiltered)
                : OperationResult<TEntity>.Fail("Entity not found.");
        }

        public virtual IQueryable<TResult> GetProjectedByFilter<TResult>(
            List<FilterCondition<TEntity>> conditions,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection,
            List<string> includeProperties = null)
        {
            var query = GetQueryableByFilter(conditions, includeProperties);
            return projection(query);
        }


        public virtual IQueryable<TEntity> GetQueryableByFilter(
            List<FilterCondition<TEntity>> conditions = null,
            List<string> includeProperties = null)
        {
            IQueryable<TEntity> query = _dbSet.AsQueryable();

            if (includeProperties != null)
            {
                foreach (var include in includeProperties)
                    query = query.Include(include);
            }

            if (conditions == null || !conditions.Any())
                return query;

            var allFilters = BuildFilters(conditions);

            if (allFilters == null || !allFilters.Any())
                return query;

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression predicate = null;

            foreach (var condition in allFilters)
            {
                if (condition.Value is DateTime dateValue)
                {
                    condition.Value = NormalizeDateTimeIfNeeded(dateValue);
                }

                var expression = BuildExpression(parameter, condition);

                predicate = predicate == null
                    ? expression
                    : (condition.IsOr == true
                        ? Expression.OrElse(predicate, expression)
                        : Expression.AndAlso(predicate, expression));
            }

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            return query.Where(lambda);
        }

        protected Expression BuildExpression(ParameterExpression parameter, FilterCondition<TEntity> condition)
        {
            MemberExpression property;

            if (condition.PropertyExpression != null)
            {
                if (parameter != null)
                    property = GetMemberExpression(parameter, condition.PropertyExpression);
                else
                    property = GetMemberExpression(condition.PropertyExpression);
            }
            else if (!string.IsNullOrEmpty(condition.PropertyName))
            {
                property = Expression.Property(parameter, condition.PropertyName);
            }
            else
            {
                throw new InvalidOperationException("PropertyExpression or PropertyName must be provided.");
            }

            Expression expression = null;
            var constant = Expression.Constant(condition.Value);

            switch (condition.Operator)
            {
                case FilterOperator.Equal:
                    expression = Expression.Equal(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.NotEqual:
                    expression = Expression.NotEqual(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.Contains:
                    expression = Expression.Call(property, nameof(string.Contains), null, Expression.Convert(constant, typeof(string)));
                    break;

                case FilterOperator.NotLike:
                    var notLike = Expression.Call(property, nameof(string.Contains), null, Expression.Convert(constant, typeof(string)));
                    expression = Expression.Not(notLike);
                    break;

                case FilterOperator.StartsWith:
                    expression = Expression.Call(property, nameof(string.StartsWith), null, Expression.Convert(constant, typeof(string)));
                    break;

                case FilterOperator.EndsWith:
                    expression = Expression.Call(property, nameof(string.EndsWith), null, Expression.Convert(constant, typeof(string)));
                    break;

                case FilterOperator.GreaterThan:
                    expression = Expression.GreaterThan(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.GreaterThanOrEqual:
                    expression = Expression.GreaterThanOrEqual(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.LessThan:
                    expression = Expression.LessThan(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.LessThanOrEqual:
                    expression = Expression.LessThanOrEqual(property, Expression.Convert(constant, property.Type));
                    break;

                case FilterOperator.In:
                case FilterOperator.NotIn:
                    {
                        if (condition.Value is not IEnumerable rawList)
                            throw new ArgumentException("Value must be an IEnumerable for In/NotIn");

                        var castedList = rawList.CastToList(property.Type);
                        var listExpression = Expression.Constant(castedList);

                        var containsMethod = typeof(Enumerable)
                            .GetMethods()
                            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);

                        var containsCall = Expression.Call(containsMethod, listExpression, property);
                        expression = condition.Operator == FilterOperator.In ? containsCall : Expression.Not(containsCall);
                        break;
                    }

                case FilterOperator.IsNull:
                    expression = Expression.Equal(property, Expression.Constant(null, property.Type));
                    break;

                case FilterOperator.IsNotNull:
                    expression = Expression.NotEqual(property, Expression.Constant(null, property.Type));
                    break;

                case FilterOperator.Between:
                    {
                        object? item1 = null;
                        object? item2 = null;

                        var valueType = condition.Value.GetType();
                        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(ValueTuple<,>))
                        {
                            item1 = valueType.GetField("Item1")?.GetValue(condition.Value);
                            item2 = valueType.GetField("Item2")?.GetValue(condition.Value);
                        }
                        else if (condition.Value is Tuple<object, object> tuple)
                        {
                            item1 = tuple.Item1;
                            item2 = tuple.Item2;
                        }
                        else
                        {
                            throw new ArgumentException("Value must be ValueTuple<T1,T2> or Tuple<object,object> for Between");
                        }

                        var targetType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

                        var startValue = Convert.ChangeType(item1, targetType);
                        var endValue = Convert.ChangeType(item2, targetType);
                        
                        var start = Expression.Constant(startValue, property.Type);
                        var end = Expression.Constant(endValue, property.Type);

                        var gte = Expression.GreaterThanOrEqual(property, start);
                        var lte = Expression.LessThanOrEqual(property, end);

                        expression = Expression.AndAlso(gte, lte);
                        break;
                    }

                default:
                    throw new NotSupportedException($"FilterOperator '{condition.Operator}' is not supported.");
            }

            return expression;
        }

        protected virtual Expression<Func<TEntity, bool>> UniqueFilter(TEntity entity) => x => false;

        public virtual async Task<OperationResult<TEntity>> Add(TEntity entity)
        {
            try
            {
                if (await _dbSet.AnyAsync(UniqueFilter(entity)))
                    return OperationResult<TEntity>.Fail("Entity already exists.");

                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();
                return OperationResult<TEntity>.Ok(entity, "success");
            }
            catch (Exception ex)
            {
                return OperationResult<TEntity>.Fail($"Add failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public virtual async Task<OperationResult<TEntity>> Update(TEntity entity, Expression<Func<TEntity, bool>> predicate = null)
        {
            try
            {
                var keyPropertyNames = _context.Model
                    .FindEntityType(typeof(TEntity))?
                    .FindPrimaryKey()?
                    .Properties;

                var keyPropertiesList = keyPropertyNames
                    .Select(p => p.Name)
                    .ToList();
                var keyPropertiesSet = keyPropertiesList?.ToHashSet();

                if (keyPropertyNames == null || keyPropertyNames.Count == 0)
                {
                    _dbSet.Update(entity);
                    await _context.SaveChangesAsync();
                    return OperationResult<TEntity>.Ok(entity, "success");
                }

                var keyValues = keyPropertiesList?.Select(name => typeof(TEntity).GetProperty(name)?.GetValue(entity))
                    .ToArray();

                var dbEntity = await _dbSet.FindAsync(keyValues);
                if (dbEntity == null && predicate != null)
                    dbEntity = await _dbSet.FirstOrDefaultAsync(predicate);
                if (dbEntity == null && predicate == null)
                    return OperationResult<TEntity>.Fail($"Entity of type {typeof(TEntity).Name} with key not found");

                foreach (var prop in typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (keyPropertiesSet.Contains(prop.Name)) continue;

                    var newValue = prop.GetValue(entity);
                    if (newValue != null)
                    {
                        var currentValue = prop.GetValue(dbEntity);
                        if (!Equals(currentValue, newValue))
                            prop.SetValue(dbEntity, newValue);
                    }
                }

                await _context.SaveChangesAsync();
                return OperationResult<TEntity>.Ok(dbEntity, "success");
            }
            catch (Exception ex)
            {
                return OperationResult<TEntity>.Fail($"Update failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public virtual async Task<OperationResult> Delete(params object[] keyValues)
        {
            try
            {
                var entity = await _dbSet.FindAsync(keyValues);
                if (entity == null)
                    return OperationResult.Fail("Entity not found.");

                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public virtual async Task<OperationResult> Delete(TEntity entity)
        {
            try
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        
        
        public virtual async Task<OperationResult> DeleteAll(bool confirm = false)
        {
            if (!confirm)
                return OperationResult.Fail("DeleteAll requires explicit confirmation.");

            return await Delete(x => true);
        }

        #region Transaction Begin & Batch

        public virtual async Task<OperationResult<List<TEntity>>> Add(List<TEntity> entities)
        {
            try
            {
                if (entities == null || !entities.Any())
                    return OperationResult<List<TEntity>>.Fail("No entities to insert.");

                var uniqueEntities = new List<TEntity>();
                foreach (var entity in entities)
                {
                    if (!await _dbSet.AnyAsync(UniqueFilter(entity)))
                        uniqueEntities.Add(entity);
                }

                if (!uniqueEntities.Any())
                    return OperationResult<List<TEntity>>.Fail("All entities already exist.");

                await _dbSet.AddRangeAsync(uniqueEntities);
                await _context.SaveChangesAsync();

                return OperationResult<List<TEntity>>.Ok(uniqueEntities, "success");
            }
            catch (Exception ex)
            {
                return OperationResult<List<TEntity>>.Fail($"AddRange failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public virtual async Task<OperationResult> Delete(List<TEntity> entities)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _dbSet.RemoveRange(entities);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail($"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        public virtual async Task<OperationResult> Delete(Expression<Func<TEntity, bool>> predicate)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var entities = await _dbSet.Where(predicate).ToListAsync();

                if (!entities.Any())
                    return OperationResult.Fail("No matching records found to delete.");

                _dbSet.RemoveRange(entities);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail($"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        public virtual async Task<OperationResult<int>> UpdateBulkAsync(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> updateExpression)
        {
            try
            {
                var affectedRows = await _dbSet
                    .Where(predicate)
                    .ExecuteUpdateAsync(updateExpression);

                return OperationResult<int>.Ok(affectedRows, "Bulk update success");
            }
            catch (Exception ex)
            {
                return OperationResult<int>.Fail($"Bulk update failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public virtual async Task<OperationResult<List<TEntity>>> UpdateBulkAsync(List<TEntity> entities, Expression<Func<TEntity, bool>> predicate = null)
        {
            if (entities == null || entities.Count == 0)
                return OperationResult<List<TEntity>>.Fail("No entities provided.");

            try
            {
                var entityType = typeof(TEntity);
                var keyPropertyNames = _context.Model
                    .FindEntityType(entityType)?
                    .FindPrimaryKey()?
                    .Properties;

                var keyPropertiesList = keyPropertyNames?
                    .Select(p => p.Name).ToList();
                var keyPropertiesSet = keyPropertiesList?
                    .ToHashSet();

                if (keyPropertyNames == null || keyPropertyNames.Count == 0)
                {
                    _dbSet.UpdateRange(entities);
                    await _context.SaveChangesAsync();
                    return OperationResult<List<TEntity>>.Ok(entities, "Bulk update success");
                }

                var updatedEntities = new List<TEntity>();

                foreach (var entity in entities)
                {
                    var keyValues = keyPropertiesList
                        .Select(name => entityType.GetProperty(name)?.GetValue(entity))
                        .ToArray();

                    var dbEntity = await _dbSet.FindAsync(keyValues);
                    if (dbEntity == null && predicate != null)
                        dbEntity = await _dbSet.FirstOrDefaultAsync(predicate);
                    if (dbEntity == null)
                        continue;

                    foreach (var prop in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (keyPropertiesSet.Contains(prop.Name))
                            continue;

                        var newValue = prop.GetValue(entity);
                        if (newValue != null)
                        {
                            var currentValue = prop.GetValue(dbEntity);
                            if (!Equals(currentValue, newValue))
                                prop.SetValue(dbEntity, newValue);
                        }
                    }

                    updatedEntities.Add(dbEntity);
                }

                if (updatedEntities.Count > 0)
                    await _context.SaveChangesAsync();

                return OperationResult<List<TEntity>>.Ok(updatedEntities, "Bulk update success");
            }
            catch (Exception ex)
            {
                return OperationResult<List<TEntity>>.Fail($"Bulk update failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        #endregion

        public virtual OperationResult ExecuteRawSql(string sql, params object[] parameters)
        {
            try
            {
                var executeQuery = _context.Database.ExecuteSqlRaw(sql, parameters);
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Execute query failed : {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        public virtual async Task<OperationResult> ExecuteRawSqlAsync(string sql, params object[] parameters)
        {

            try
            {
                var executeQuery = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Execute query failed : {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        public virtual List<TEntity> QueryRawSql(string sql, params object[] parameters)
        {
            return _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToList();
        }
        
        public virtual async Task<List<TEntity>> QueryRawSqlAsync(string sql, params object[] parameters)
        {
            return await _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync();
        }

        public virtual async Task<List<TEntity>> QueryRawSqlInterpolatedAsync(FormattableString sql)
        {
            return await _dbSet.FromSqlInterpolated(sql).AsNoTracking().ToListAsync();
        }

        public virtual async Task<List<TEntity>> QueryRawSqlWithParamsAsync(string sql, Dictionary<string, object?> parameters)
        {
                var provider = _context.Database.ProviderName ?? "";
            var sqlParams = parameters.Select(p => ProviderParameter.Create(provider, p.Key, p.Value ?? DBNull.Value)).ToArray();
            return await _dbSet.FromSqlRaw(sql, sqlParams).AsNoTracking().ToListAsync();
        }

        public virtual OperationResult ExecuteProcedure(string procFullName, params DbParameter[] parameters)
        {
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = procFullName;
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null && parameters.Length > 0)
                {
                    foreach (var param in parameters)
                        command.Parameters.Add(param);
                }

                _context.Database.OpenConnection();
                command.ExecuteNonQuery();

                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Execute procedure failed: {ex.InnerException?.Message ?? ex.Message}");
            }
            finally
            {
                _context.Database.CloseConnection();
            }
        }
        
        public virtual OperationResult ExecuteProcedure(
            string procFullName,
            Dictionary<string, object?>? parameters = null)
        {
            try
            {
                var provider = _context.Database.ProviderName ?? "";
                string sql;

                if (parameters == null || parameters.Count == 0)
                {
                    sql = ProviderParameter.ProcedureCallSyntax(provider, procFullName, Array.Empty<string>());
                    return ExecuteRawSql(sql);
                }
                else
                {
                    var paramNames = parameters.Keys.ToList();
                    sql = ProviderParameter.ProcedureCallSyntax(provider, procFullName, paramNames);

                    var sqlParams = parameters
                        .Select(p => ProviderParameter.Create(provider, p.Key, p.Value))
                        .ToArray();

                    return ExecuteRawSql(sql, sqlParams);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    $"Execute query failed : {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        
        public virtual OperationResult ExecuteProcedureByContext(string procFullName, Dictionary<string, object?>? parameters = null)
        {
            try
            {
                using var connection = _context.Database.GetDbConnection();
                using var command = connection.CreateCommand();

                command.CommandText = procFullName;
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null && parameters.Count > 0)
                {
                    foreach (var parameter in parameters)
                    {
                        command.Parameters.Add(parameter);
                    }
                }

                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                command.ExecuteNonQuery();

                return OperationResult.Ok("Procedure executed successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"ExecuteProcedure failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        
        public async Task<T> ExecuteScalarAsync<T>(string sql, Dictionary<string, object?> parameters)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            foreach (var param in parameters)
            {
                var dbParam = command.CreateParameter();
                dbParam.ParameterName = param.Key.StartsWith("@") ? param.Key : $"@{param.Key}";
                dbParam.Value = param.Value ?? DBNull.Value;
                command.Parameters.Add(dbParam);
            }

            var result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value
                ? (T)Convert.ChangeType(result, typeof(T))
                : default!;
        }
        
        public async Task<decimal> GetNextSequenceValueAsync(string sequenceName)
        {
            string providerName = _context.Database.ProviderName ?? string.Empty;
            string sql;

            if (providerName.Contains("Npgsql"))
                sql = $"SELECT nextval('{sequenceName}')";
            else if (providerName.Contains("Oracle"))
                sql = $"SELECT {sequenceName}.NEXTVAL FROM dual";
            else if (providerName.Contains("SqlServer"))
                sql = $"SELECT NEXT VALUE FOR {sequenceName}";
            else if (providerName.Contains("MySql") || providerName.Contains("MariaDb"))
                sql = $"SELECT NEXT VALUE FOR {sequenceName}";
            else
                throw new NotSupportedException($"Sequences are not supported for provider {providerName}");

            return await ExecuteScalarAsync<decimal>(sql, new Dictionary<string, object?>());
        }

        public virtual async Task<OperationResult> ExecuteProcedureAsync(string procFullName, params DbParameter[] parameters)
        {
            try
            {
                await _context.Database.OpenConnectionAsync();

                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = procFullName;
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null && parameters.Length > 0)
                {
                    foreach (var param in parameters)
                        command.Parameters.Add(param);
                }

                await command.ExecuteNonQueryAsync();
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Execute procedure failed: {ex.InnerException?.Message ?? ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        
        protected bool IsValidColumn<TEntity>(string columnName)
        {
            var entityType = _context.Model.FindEntityType(typeof(TEntity));
            if (entityType == null) return false;

            var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName(), entityType.GetSchema());
            var validColumns = entityType
                .GetProperties()
                .Select(p => p.GetColumnName(storeObject))
                .Where(name => name != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return validColumns.Contains(columnName);
        }

        public virtual DbParameter CreateParameter(string name, object? value, DbType? type = null)
        {
            var command = _context.Database.GetDbConnection().CreateCommand();
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            if (type.HasValue)
                parameter.DbType = type.Value;
            return parameter;
        }

        protected virtual List<FilterCondition<TEntity>> DefaultFilters() => new();

        protected virtual List<FilterCondition<TEntity>> BuildFilters(List<FilterCondition<TEntity>> userFilters = null)
        {
            var combined = new List<FilterCondition<TEntity>>();

            var defaultFilters = DefaultFilters();
            if (defaultFilters?.Any() == true)
                combined.AddRange(defaultFilters);

            if (userFilters?.Any() == true)
                combined.AddRange(userFilters);

            return combined;
        }
        protected virtual List<FilterCondition<TEntity>> BuildDynamicFilters(Action<FilterBuilder<TEntity>> build)
        {
            var builder = new FilterBuilder<TEntity>();
            build(builder);

            var normalizedFilters = builder.ToArray()
                .Where(f => f.Value != null && (!(f.Value is string s) || !string.IsNullOrWhiteSpace(s)))
                .Select(f => new NormalizedFilter(
                    Property: f.Property,
                    Value: f.Value!,
                    Operator: f.Operator,
                    IsOr: f.IsOr
                ));

            return FilterConditions(normalizedFilters);
        }



        //protected record FilterInput<TProp>(
        //    Expression<Func<TEntity, TProp>> Property,
        //    object? Value,
        //    FilterOperator Operator,
        //    bool IsOr = false
        //);

        protected record FilterInput<TProp>(
            Expression<Func<TEntity, TProp>> Property,
            object? Value,
            (TProp, TProp)? Range,
            FilterOperator Operator,
            bool IsOr = false
        )
        {
            public FilterInput(Expression<Func<TEntity, TProp>> property, object? value, FilterOperator op, bool isOr = false)
                : this(property, value, null, op, isOr) { }

            public FilterInput(Expression<Func<TEntity, TProp>> property, (TProp, TProp) range, FilterOperator op, bool isOr = false)
                : this(property, null, range, op, isOr) { }
        }

        protected record NormalizedFilter(
            Expression<Func<TEntity, object>> Property,
            object Value,
            FilterOperator Operator,
            bool IsOr);

        //protected NormalizedFilter NormalizeFilter<TProp>(FilterInput<TProp> input)
        //{
        //    object? value = input.Value;

        //    if 
        //        (input.Value is string s && string.IsNullOrWhiteSpace(s))
        //        value = null;

        //    var targetType = typeof(TProp);
        //    if (Nullable.GetUnderlyingType(targetType) is Type underlyingType)
        //    {
        //        targetType = underlyingType;
        //    }
        //    if (value != null && value.GetType() != targetType)
        //    {
        //        try
        //        {
        //            value = Convert.ChangeType(value, targetType);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new InvalidCastException($"Cannot convert value '{value}' to type '{targetType.Name}'", ex);
        //        }
        //    }

        //    return new NormalizedFilter(
        //        Property: CastToObject(input.Property),
        //        Value: input.Value!,
        //        Operator: input.Operator,
        //        IsOr: input.IsOr);
        //}

        protected NormalizedFilter NormalizeFilter<TProp>(FilterInput<TProp> input)
        {
            var targetType = typeof(TProp);
            if (Nullable.GetUnderlyingType(targetType) is Type underlyingType)
                targetType = underlyingType;

            if (input.Range.HasValue)
            {
                var (from, to) = input.Range.Value;
                object fromVal, toVal;

                try
                {
                    fromVal = Convert.ChangeType(from, targetType)!;
                    toVal = Convert.ChangeType(to, targetType)!;
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException($"Cannot convert range value ({from}, {to}) to type '{targetType.Name}'", ex);
                }

                return new NormalizedFilter(
                    Property: CastToObject(input.Property),
                    Value: (fromVal, toVal),
                    Operator: input.Operator,
                    IsOr: input.IsOr
                );
            }

            if (input.Operator is FilterOperator.In or FilterOperator.NotIn)
            {
                if (input.Value is not IEnumerable rawList)
                    throw new InvalidCastException("IN/NOT IN filters require a collection");

                var castedList = rawList.CastToList(targetType);
                return new NormalizedFilter(
                    Property: CastToObject(input.Property),
                    Value: castedList,
                    Operator: input.Operator,
                    IsOr: input.IsOr
                );
            }

            object? value = input.Value;

            if (value is string s && string.IsNullOrWhiteSpace(s))
                value = null;

            if (value != null && value.GetType() != targetType)
            {
                try
                {
                    value = Convert.ChangeType(value, targetType);
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException($"Cannot convert value '{value}' to type '{targetType.Name}'", ex);
                }
            }

            return new NormalizedFilter(
                Property: CastToObject(input.Property),
                Value: value!,
                Operator: input.Operator,
                IsOr: input.IsOr
            );
        }


        protected List<FilterCondition<TEntity>> FilterConditions(IEnumerable<NormalizedFilter> normalizedFilters)
        {
            return normalizedFilters.Select(f =>
                {
                    if (f.Operator is FilterOperator.GroupOr or FilterOperator.GroupAnd)
                    {
                        var groupFilters = (f.Value as IEnumerable<NormalizedFilter>)!;
                        return new FilterCondition<TEntity>
                        {
                            Operator = f.Operator,
                            GroupConditions = FilterConditions(groupFilters)
                        };
                    }

                    return new FilterCondition<TEntity>
                    {
                        PropertyExpression = f.Property,
                        Value = f.Value,
                        Operator = f.Operator,
                        IsOr = f.IsOr
                    };
                })
                .Where(f =>
                    (f.GroupConditions?.Any() == true) ||
                    (f.Value != null && (!(f.Value is string s) || !string.IsNullOrWhiteSpace(s))))
                .ToList();
            
            // return normalizedFilters
            //     .Where(f => f.Value != null && (!(f.Value is string s) || !string.IsNullOrWhiteSpace(s)))
            //     .Select(f => new FilterCondition<TEntity>
            //     {
            //         PropertyExpression = f.Property,
            //         Value = f.Value,
            //         Operator = f.Operator,
            //         IsOr = f.IsOr
            //     })
            //     .ToList();
        }

        protected List<FilterCondition<TEntity>> FilterConditions<TProp>(params FilterInput<TProp>[] filters)
        {
            var normalized = filters
                .Where(f => f.Value != null && (!(f.Value is string s) || !string.IsNullOrWhiteSpace(s)))
                .Select(NormalizeFilter);

            return FilterConditions(normalized);
        }

        protected List<FilterCondition<TEntity>> FilterConditions(params NormalizedFilter[] filters)
        {
            return FilterConditions((IEnumerable<NormalizedFilter>)filters);
        }

        protected List<FilterCondition<TEntity>> FilterCondition<TProp>(
            Expression<Func<TEntity, TProp>> property,
            object? value,
            FilterOperator op,
            bool isOr = false)
        {
            var normalized = NormalizeFilter(new FilterInput<TProp>(property, value, op, isOr));
            return FilterConditions(normalized);
        }

        protected NormalizedFilter Filter<TProp>(
            Expression<Func<TEntity, TProp>> property,
            object? value,
            FilterOperator op,
            bool isOr = false)
        {
            return NormalizeFilter(new FilterInput<TProp>(property, value, op, isOr));
        }

        protected NormalizedFilter Filter<TProp>(
            Expression<Func<TEntity, TProp>> property,
            (TProp Start, TProp End) value,
            FilterOperator op,
            bool isOr = false)
        {
            return NormalizeFilter(new FilterInput<TProp>(property, value, op, isOr));
        }

        protected NormalizedFilter FilterDateOnly(Expression<Func<TEntity, DateTime>> property, DateTime date)
        {
            var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            var end = start.AddDays(1);
            return Filter(property, (start, end), FilterOperator.Between);
        }

        protected NormalizedFilter FilterList<TProp>(
        Expression<Func<TEntity, TProp>> property,
        IEnumerable<TProp> values,
        FilterOperator op = FilterOperator.In,
        bool isOr = false)
        {
            return NormalizeFilter(new FilterInput<TProp>(property, values, op, isOr));
        }
        
        protected NormalizedFilter FilterGroup(FilterGroupOperator groupOperator, params NormalizedFilter[] filters)
        {
            FilterOperator groupOp;
            switch (groupOperator)
            {
                case FilterGroupOperator.And:
                    groupOp = FilterOperator.GroupAnd;
                    break;
                case FilterGroupOperator.Or:
                    groupOp = FilterOperator.GroupOr;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported group operator: {groupOperator}");
            }

            return new NormalizedFilter(
                Property: null!,
                Value: filters.ToList(),
                Operator: groupOp,
                IsOr: false
            );
        }

        protected Expression<Func<TEntity, object>> CastToObject<TProp>(Expression<Func<TEntity, TProp>> expr)
        {
            var parameter = expr.Parameters[0];
            Expression body = expr.Body.Type == typeof(object)
                ? expr.Body
                : Expression.Convert(expr.Body, typeof(object));
            return Expression.Lambda<Func<TEntity, object>>(body, parameter);
        }


        protected IMapper GetSafeMapper<TEntity, TDestination>() =>
            MappingConfigProvider.ToSafeMapper<TEntity, TDestination>();

        protected IMapper GetPlainMapper<TEntity, TDestination>() =>
            MappingConfigProvider.ToPlainMapper<TEntity, TDestination>();

        protected IMapper GetSelfMapper<TEntity>() =>
            MappingConfigProvider.ToSelfMapper<TEntity>();

        public virtual List<TDestination> GetSafeMappedList<TDestination>(Func<TEntity, bool> predicate = null)
            => GetMappedListWithMapper<TEntity, TDestination>(() => GetSafeMapper<TEntity, TDestination>(), predicate);

        public virtual List<TDestination> GetPlainMappedList<TDestination>(Func<TEntity, bool> predicate = null)
            => GetMappedListWithMapper<TEntity, TDestination>(() => GetPlainMapper<TEntity, TDestination>(), predicate);

        private List<TDestination> GetMappedListWithMapper<TSource, TDestination>(
            Func<IMapper> mapperFactory,
            Func<TEntity, bool> predicate = null)
        {
            var mapper = mapperFactory();
            var query = _dbSet.AsNoTracking();

            var list = predicate == null
                ? query.ToList()
                : query.ToList().Where(predicate).ToList();

            return list.Select(e => mapper.Map<TDestination>(e)).ToList();
        }

        private static MemberExpression GetMemberExpression(Expression<Func<TEntity, object>> expr)
        {
            return expr.Body switch
            {
                UnaryExpression unary when unary.Operand is MemberExpression me => me,
                MemberExpression me => me,
                _ => throw new InvalidOperationException("Invalid expression")
            };
        }

        private MemberExpression GetMemberExpression<TEntity>(
            ParameterExpression parameter,
            Expression<Func<TEntity, object>> propertyExpression)
        {
            var body = propertyExpression.Body;

            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member)
                return Expression.PropertyOrField(parameter, member.Member.Name);

            throw new InvalidOperationException("Invalid property expression");
        }
        
        private static MemberExpression GetMemberExpression<TEntity, TProperty>(
            ParameterExpression parameter,
            Expression<Func<TEntity, TProperty>> propertyExpression)
        {
            Expression body = propertyExpression.Body;

            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member)
                return Expression.PropertyOrField(parameter, member.Member.Name);

            throw new InvalidOperationException("Invalid property expression");
        }

        private IReadOnlyList<IProperty> GetKeyProperties()
        {
            return _keyPropertyCache.GetOrAdd(typeof(TEntity), t =>
            {
                var entityType = _context.Model.FindEntityType(t);
                return entityType?.FindPrimaryKey()?.Properties
                       ?? throw new InvalidOperationException($"No primary key defined for entity {t.Name}");
            });
        }

        private static DateTime NormalizeDateTimeIfNeeded(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Local)
                return dt.ToUniversalTime();
            if (dt.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            if (dt.Kind == DateTimeKind.Utc)
                return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            return dt;
        }
    }
}
