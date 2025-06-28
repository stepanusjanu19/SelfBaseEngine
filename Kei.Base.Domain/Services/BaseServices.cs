using Kei.Base.Domain.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Services
{
    public abstract class BaseServices<TEntity> where TEntity : class
    {
        protected readonly BaseRepository<TEntity> _repository;

        protected BaseServices(BaseRepository<TEntity> repository)
        {
            _repository = repository;
        }
    }
}
