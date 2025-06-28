using AutoMapper;
using Kei.Base.Domain.Functions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Repository
{
    public abstract class BaseRepository<TEntity> : BaseFunctions<TEntity> where TEntity : class
    {
        protected BaseRepository(DbContext context) 
            : base(context)
        { }
    }
}
