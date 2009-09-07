using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Common;
using ServiceStack.Common.Extensions;
using ServiceStack.Common.Utils;
using ServiceStack.DataAccess;

namespace ServiceStack.CacheAccess.Providers
{
	public abstract class BasicPersistenceProviderCacheBase : IPersistenceProviderCache
	{
		protected ICacheClient CacheClient { get; set; }

		protected BasicPersistenceProviderCacheBase(ICacheClient cacheClient)
		{
			CacheClient = cacheClient;
		}

		protected abstract IBasicPersistenceProvider GetBasicPersistenceProvider();

		public TEntity GetById<TEntity>(object entityId)
			where TEntity : class, new()
		{
			var cacheKey = IdUtils.CreateUrn<TEntity>(entityId);
			var cacheEntity = this.CacheClient.Get<TEntity>(cacheKey);
			if (Equals(cacheEntity, default(TEntity)))
			{
				using (var db = GetBasicPersistenceProvider())
				{
					cacheEntity = db.GetById<TEntity>(entityId);
					this.SetCache(cacheKey, cacheEntity);
				}
			}
			return cacheEntity;
		}

		public void SetCache<TEntity>(TEntity entity)
			where TEntity : class, new()
		{
			var cacheKey = entity.CreateUrn();
			this.SetCache(cacheKey, entity);
		}

		private void SetCache<TEntity>(string cacheKey, TEntity entity)
		{
			this.CacheClient.Set(cacheKey, entity);
		}

		public void Store<TEntity>(TEntity entity)
			where TEntity : class, new()
		{
			var cacheKey = entity.CreateUrn();
			using (var db = GetBasicPersistenceProvider())
			{
				db.Store(entity);
				this.SetCache(cacheKey, entity);
			}
		}

		public List<TEntity> GetByIds<TEntity>(ICollection entityIds)
			where TEntity : class, new()
		{
			if (entityIds.Count == 0) return new List<TEntity>();

			var cacheKeys = entityIds.ConvertAll(x => x.CreateUrn());
			var cacheEntitiesMap = this.CacheClient.GetAll<TEntity>(cacheKeys);

			if (cacheEntitiesMap.Count < entityIds.Count)
			{
				var entityIdType = entityIds.First().GetType();

				var entityIdsNotInCache = cacheKeys
					.Where(x => !cacheEntitiesMap.ContainsKey(x))
					.ConvertAll(x => 
						StringConverterUtils.Parse(UrnId.GetStringId(x), entityIdType));

				using (var db = GetBasicPersistenceProvider())
				{
					var cacheEntities = db.GetByIds<TEntity>(entityIdsNotInCache);

					foreach (var cacheEntity in cacheEntities)
					{
						var cacheKey = cacheEntity.CreateUrn();
						this.CacheClient.Set(cacheKey, cacheEntity);
						cacheEntitiesMap[cacheKey] = cacheEntity;
					}
				}
			}

			return cacheEntitiesMap.Values.ToList();
		}

		public void ClearAll<TEntity>(ICollection entityIds)
			where TEntity : class, new()
		{
			var cacheKeys = entityIds.ConvertAll(x => IdUtils.CreateUrn<TEntity>(x));
			this.CacheClient.RemoveAll(cacheKeys);
		}

		public void Clear<TEntity>(params object[] entityIds)
			where TEntity : class, new()
		{
			this.ClearAll<TEntity>(entityIds.ToList());
		}
	}
}