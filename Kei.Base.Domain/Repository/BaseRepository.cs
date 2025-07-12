using AutoMapper;
using Kei.Base.Domain.Functions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Kei.Base.Helper;

namespace Kei.Base.Domain.Repository
{
    public abstract class BaseRepository<TEntity> : BaseFunctions<TEntity>, IBaseRepository<TEntity> where TEntity : class
    {
        protected BaseRepository(DbContext context) 
            : base(context)
        { }

        public virtual Expression<Func<TEntity, bool>> UniqueFilter(TEntity entity)
            => base.UniqueFilter(entity);

        public virtual List<FilterCondition<TEntity>> BuildFilters(List<FilterCondition<TEntity>> userFilters = null)
            => base.BuildFilters(userFilters);

        public virtual List<FilterCondition<TEntity>> BuildDynamicFilters(Action<FilterBuilder<TEntity>> build)
            => base.BuildDynamicFilters(build);
    }
}
