﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AsyncGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NHibernate.Action;
using NHibernate.Cache;
using NHibernate.Cache.Access;
using NHibernate.Cache.Entry;
using NHibernate.Engine;
using NHibernate.Impl;
using NHibernate.Persister.Entity;
using NHibernate.Proxy;
using NHibernate.Type;

namespace NHibernate.Event.Default
{
	using System.Threading.Tasks;
	using System.Threading;
	public partial class DefaultLoadEventListener : AbstractLockUpgradeEventListener, ILoadEventListener
	{

		public virtual async Task OnLoadAsync(LoadEvent @event, LoadType loadType, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ISessionImplementor source = @event.Session;

			IEntityPersister persister;
			if (@event.InstanceToLoad != null)
			{
				@event.EntityClassName = source.BestGuessEntityName(@event.InstanceToLoad); //the load() which takes an entity does not pass an entityName
				persister = source.GetEntityPersister(@event.EntityClassName, @event.InstanceToLoad);
			}
			else
			{
				persister = GetEntityPersister(source.Factory, @event.EntityClassName);
			}

			if (persister == null)
			{
				var message = new StringBuilder(512);
				message.AppendLine(string.Format("Unable to locate persister for the entity named '{0}'.", @event.EntityClassName));
				message.AppendLine("The persister define the persistence strategy for an entity.");
				message.AppendLine("Possible causes:");
				message.AppendLine(string.Format(" - The mapping for '{0}' was not added to the NHibernate configuration.", @event.EntityClassName));
				throw new HibernateException(message.ToString());
			}

			if (persister.IdentifierType.IsComponentType)
			{
				// skip this check for composite-ids relating to dom4j entity-mode;
				// alternatively, we could add a check to make sure the incoming id value is
				// an instance of Element...
			}
			else
			{
				System.Type idClass = persister.IdentifierType.ReturnedClass;
				if (idClass != null && !idClass.IsInstanceOfType(@event.EntityId) &&
					!(@event.EntityId is DelayedPostInsertIdentifier))
				{
					throw new TypeMismatchException("Provided id of the wrong type. Expected: " + idClass + ", got " + @event.EntityId.GetType());
				}
			}

			EntityKey keyToLoad = source.GenerateEntityKey(@event.EntityId, persister);
			try
			{
				if (loadType.IsNakedEntityReturned)
				{
					//do not return a proxy!
					//(this option indicates we are initializing a proxy)
					@event.Result = await (LoadAsync(@event, persister, keyToLoad, loadType, cancellationToken)).ConfigureAwait(false);
				}
				else
				{
					//return a proxy if appropriate
					if (@event.LockMode == LockMode.None)
					{
						@event.Result = await (ProxyOrLoadAsync(@event, persister, keyToLoad, loadType, cancellationToken)).ConfigureAwait(false);
					}
					else
					{
						@event.Result = await (LockAndLoadAsync(@event, persister, keyToLoad, loadType, source, cancellationToken)).ConfigureAwait(false);
					}
				}
			}
			catch (HibernateException e)
			{
				log.Info(e, "Error performing load command");
				throw;
			}
		}

		/// <summary> Perfoms the load of an entity. </summary>
		/// <returns> The loaded entity. </returns>
		protected virtual async Task<object> LoadAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (@event.InstanceToLoad != null)
			{
				if (@event.Session.PersistenceContext.GetEntry(@event.InstanceToLoad) != null)
				{
					throw new PersistentObjectException("attempted to load into an instance that was already associated with the session: " + MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
				}
				persister.SetIdentifier(@event.InstanceToLoad, @event.EntityId);
			}

			object entity = await (DoLoadAsync(@event, persister, keyToLoad, options, cancellationToken)).ConfigureAwait(false);

			bool isOptionalInstance = @event.InstanceToLoad != null;

			if (!options.IsAllowNulls || isOptionalInstance)
			{
				if (entity == null)
				{
					@event.Session.Factory.EntityNotFoundDelegate.HandleEntityNotFound(@event.EntityClassName, @event.EntityId);
				}
			}

			if (isOptionalInstance && entity != @event.InstanceToLoad)
			{
				throw new NonUniqueObjectException(@event.EntityId, @event.EntityClassName);
			}

			return entity;
		}

		/// <summary>
		/// Based on configured options, will either return a pre-existing proxy,
		/// generate a new proxy, or perform an actual load.
		/// </summary>
		/// <returns> The result of the proxy/load operation.</returns>
		protected virtual Task<object> ProxyOrLoadAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			try
			{
				if (log.IsDebugEnabled())
				{
					log.Debug("loading entity: {0}", MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
				}

				if (!persister.HasProxy)
				{
					// this class has no proxies (so do a shortcut)
					return LoadAsync(@event, persister, keyToLoad, options, cancellationToken);
				}
				else
				{
					IPersistenceContext persistenceContext = @event.Session.PersistenceContext;

					// look for a proxy
					object proxy = persistenceContext.GetProxy(keyToLoad);
					if (proxy != null)
					{
						return ReturnNarrowedProxyAsync(@event, persister, keyToLoad, options, persistenceContext, proxy, cancellationToken);
					}
					else
					{
						if (options.IsAllowProxyCreation)
						{
							return Task.FromResult<object>(CreateProxyIfNecessary(@event, persister, keyToLoad, options, persistenceContext));
						}
						else
						{
							// return a newly loaded object
							return LoadAsync(@event, persister, keyToLoad, options, cancellationToken);
						}
					}
				}
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		/// <summary>
		/// Given that there is a pre-existing proxy.
		/// Initialize it if necessary; narrow if necessary.
		/// </summary>
		private async Task<object> ReturnNarrowedProxyAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, IPersistenceContext persistenceContext, object proxy, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			log.Debug("entity proxy found in session cache");
			var castedProxy = (INHibernateProxy) proxy;
			ILazyInitializer li = castedProxy.HibernateLazyInitializer;
			if (li.Unwrap)
			{
				return await (li.GetImplementationAsync(cancellationToken)).ConfigureAwait(false);
			}
			object impl = null;
			if (!options.IsAllowProxyCreation)
			{
				impl = await (LoadAsync(@event, persister, keyToLoad, options, cancellationToken)).ConfigureAwait(false);
				// NH Different behavior : NH-1252
				if (impl == null && !options.IsAllowNulls)
				{
					@event.Session.Factory.EntityNotFoundDelegate.HandleEntityNotFound(persister.EntityName, keyToLoad.Identifier);
				}
			}
			if (impl == null && !options.IsAllowProxyCreation && options.ExactPersister)
			{
				// NH Different behavior : NH-1252
				return null;
			}
			return persistenceContext.NarrowProxy(castedProxy, persister, keyToLoad, impl);
		}

		/// <summary>
		/// If the class to be loaded has been configured with a cache, then lock
		/// given id in that cache and then perform the load.
		/// </summary>
		/// <returns> The loaded entity </returns>
		protected virtual async Task<object> LockAndLoadAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, ISessionImplementor source, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ISoftLock sLock = null;
			CacheKey ck;
			if (persister.HasCache)
			{
				ck = source.GenerateCacheKey(@event.EntityId, persister.IdentifierType, persister.RootEntityName);
				sLock = await (persister.Cache.LockAsync(ck, null, cancellationToken)).ConfigureAwait(false);
			}
			else
			{
				ck = null;
			}

			object entity;
			try
			{
				entity = await (LoadAsync(@event, persister, keyToLoad, options, cancellationToken)).ConfigureAwait(false);
			}
			finally
			{
				if (persister.HasCache)
				{
					await (persister.Cache.ReleaseAsync(ck, sLock, cancellationToken)).ConfigureAwait(false);
				}
			}

			object proxy = @event.Session.PersistenceContext.ProxyFor(persister, keyToLoad, entity);

			return proxy;
		}
		/// <summary>
		/// Coordinates the efforts to load a given entity.  First, an attempt is
		/// made to load the entity from the session-level cache.  If not found there,
		/// an attempt is made to locate it in second-level cache.  Lastly, an
		/// attempt is made to load it directly from the datasource.
		/// </summary>
		/// <param name="event">The load event </param>
		/// <param name="persister">The persister for the entity being requested for load </param>
		/// <param name="keyToLoad">The EntityKey representing the entity to be loaded. </param>
		/// <param name="options">The load options. </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> The loaded entity, or null. </returns>
		protected virtual async Task<object> DoLoadAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (log.IsDebugEnabled())
			{
				log.Debug("attempting to resolve: {0}", MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
			}

			object entity = await (LoadFromSessionCacheAsync(@event, keyToLoad, options, cancellationToken)).ConfigureAwait(false);
			if (entity == RemovedEntityMarker)
			{
				log.Debug("load request found matching entity in context, but it is scheduled for removal; returning null");
				return null;
			}
			if (entity == InconsistentRTNClassMarker)
			{
				log.Debug("load request found matching entity in context, but the matched entity was of an inconsistent return type; returning null");
				return null;
			}
			if (entity != null)
			{
				if (log.IsDebugEnabled())
				{
					log.Debug("resolved object in session cache: {0}", MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
				}
				return entity;
			}

			entity = await (LoadFromSecondLevelCacheAsync(@event, persister, options, cancellationToken)).ConfigureAwait(false);
			if (entity != null)
			{
				if (log.IsDebugEnabled())
				{
					log.Debug("resolved object in second-level cache: {0}", MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
				}
				return entity;
			}

			if (log.IsDebugEnabled())
			{
				log.Debug("object not resolved in any cache: {0}", MessageHelper.InfoString(persister, @event.EntityId, @event.Session.Factory));
			}

			return await (LoadFromDatasourceAsync(@event, persister, keyToLoad, options, false, cancellationToken)).ConfigureAwait(false);
		}

		/// <summary>
		/// Performs the process of loading an entity from the configured underlying datasource.
		/// </summary>
		/// <param name="event">The load event </param>
		/// <param name="persister">The persister for the entity being requested for load </param>
		/// <param name="keyToLoad">The EntityKey representing the entity to be loaded. </param>
		/// <param name="options">The load options. </param>
		/// <param name="checkCache">A flag to check the cache or not</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> The object loaded from the datasource, or null if not found. </returns>
		protected virtual async Task<object> LoadFromDatasourceAsync(LoadEvent @event, IEntityPersister persister, EntityKey keyToLoad, LoadType options, bool checkCache, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ISessionImplementor source = @event.Session;

			Stopwatch stopWatch = null;
			if (source.Factory.Statistics.IsStatisticsEnabled)
			{
				stopWatch = Stopwatch.StartNew();
			}

			object entity = await (persister.LoadAsync(@event.EntityId, @event.InstanceToLoad, @event.LockMode, source, checkCache, cancellationToken)).ConfigureAwait(false);

			if (stopWatch != null && @event.IsAssociationFetch)
			{
				stopWatch.Stop();
				source.Factory.StatisticsImplementor.FetchEntity(@event.EntityClassName, stopWatch.Elapsed);
			}

			return entity;
		}

		/// <summary>
		/// Attempts to locate the entity in the session-level cache.
		/// </summary>
		/// <param name="event">The load event </param>
		/// <param name="keyToLoad">The EntityKey representing the entity to be loaded. </param>
		/// <param name="options">The load options. </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> The entity from the session-level cache, or null. </returns>
		/// <remarks>
		/// If allowed to return nulls, then if the entity happens to be found in
		/// the session cache, we check the entity type for proper handling
		/// of entity hierarchies.
		/// If checkDeleted was set to true, then if the entity is found in the
		/// session-level cache, it's current status within the session cache
		/// is checked to see if it has previously been scheduled for deletion.
		/// </remarks>
		protected virtual async Task<object> LoadFromSessionCacheAsync(LoadEvent @event, EntityKey keyToLoad, LoadType options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ISessionImplementor session = @event.Session;
			object old = await (session.GetEntityUsingInterceptorAsync(keyToLoad, cancellationToken)).ConfigureAwait(false);

			if (old != null)
			{
				// this object was already loaded
				EntityEntry oldEntry = session.PersistenceContext.GetEntry(old);
				if (options.IsCheckDeleted)
				{
					Status status = oldEntry.Status;
					if (status == Status.Deleted || status == Status.Gone)
					{
						return RemovedEntityMarker;
					}
				}
				if (options.IsAllowNulls)
				{
					IEntityPersister persister = GetEntityPersister(@event.Session.Factory, @event.EntityClassName);
					if (!persister.IsInstance(old))
					{
						return InconsistentRTNClassMarker;
					}
				}
				await (UpgradeLockAsync(old, oldEntry, @event.LockMode, session, cancellationToken)).ConfigureAwait(false);
			}
			return old;
		}

		/// <summary> Attempts to load the entity from the second-level cache. </summary>
		/// <param name="event">The load event </param>
		/// <param name="persister">The persister for the entity being requested for load </param>
		/// <param name="options">The load options. </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> The entity from the second-level cache, or null. </returns>
		protected virtual async Task<object> LoadFromSecondLevelCacheAsync(LoadEvent @event, IEntityPersister persister, LoadType options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ISessionImplementor source = @event.Session;
			bool useCache = persister.HasCache && source.CacheMode .HasFlag(CacheMode.Get)
				&& @event.LockMode.LessThan(LockMode.Read);

			if (!useCache)
			{
				return null;
			}
			ISessionFactoryImplementor factory = source.Factory;
			var batchSize = persister.GetBatchSize();
			var entityBatch = source.PersistenceContext.BatchFetchQueue.QueryCacheQueue
			                        ?.GetEntityBatch(persister, @event.EntityId);
			if (entityBatch != null || batchSize > 1 && persister.Cache.PreferMultipleGet())
			{
				// The first item in the array is the item that we want to load
				if (entityBatch != null)
				{
					if (entityBatch.Length == 0)
					{
						return null; // The key was already checked
					}

					batchSize = entityBatch.Length;
				}

				if (entityBatch == null)
				{
					entityBatch = await (source.PersistenceContext.BatchFetchQueue.GetEntityBatchAsync(persister, @event.EntityId, batchSize, false, cancellationToken)).ConfigureAwait(false);
				}

				// Ignore null values as the retrieved batch may contains them when there are not enough
				// uninitialized entities in the queue
				var keys = new List<CacheKey>(batchSize);
				for (var i = 0; i < entityBatch.Length; i++)
				{
					var key = entityBatch[i];
					if (key == null)
					{
						break;
					}
					keys.Add(source.GenerateCacheKey(key, persister.IdentifierType, persister.RootEntityName));
				}
				var cachedObjects = await (persister.Cache.GetManyAsync(keys.ToArray(), source.Timestamp, cancellationToken)).ConfigureAwait(false);
				for (var i = 1; i < cachedObjects.Length; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await (AssembleAsync(
						keys[i],
						cachedObjects[i],
						new LoadEvent(entityBatch[i], @event.EntityClassName, @event.LockMode, @event.Session),
						false)).ConfigureAwait(false);
				}
				cancellationToken.ThrowIfCancellationRequested();
				return await (AssembleAsync(keys[0], cachedObjects[0], @event, true)).ConfigureAwait(false);
			}
			var cacheKey = source.GenerateCacheKey(@event.EntityId, persister.IdentifierType, persister.RootEntityName);
			var cachedObject = await (persister.Cache.GetAsync(cacheKey, source.Timestamp, cancellationToken)).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return await (AssembleAsync(cacheKey, cachedObject, @event, true)).ConfigureAwait(false);

			Task<object> AssembleAsync(CacheKey ck, object ce, LoadEvent evt, bool alterStatistics)
			{
				try
				{
					if (factory.Statistics.IsStatisticsEnabled && alterStatistics)
					{
						if (ce == null)
						{
							factory.StatisticsImplementor.SecondLevelCacheMiss(persister.Cache.RegionName);
							log.Debug("Entity cache miss: {0}", ck);
						}
						else
						{
							factory.StatisticsImplementor.SecondLevelCacheHit(persister.Cache.RegionName);
							log.Debug("Entity cache hit: {0}", ck);
						}
					}

					if (ce != null)
					{
						CacheEntry entry = (CacheEntry) persister.CacheEntryStructure.Destructure(ce, factory);

						// Entity was found in second-level cache...
						// NH: Different behavior (take a look to options.ExactPersister (NH-295))
						if (!options.ExactPersister || persister.EntityMetamodel.SubclassEntityNames.Contains(entry.Subclass))
						{
							return AssembleCacheEntryAsync(entry, evt.EntityId, persister, evt, cancellationToken);
						}
					}

					return Task.FromResult<object>(null);
				}
				catch (Exception ex)
				{
					return Task.FromException<object>(ex);
				}
			}
		}

		private async Task<object> AssembleCacheEntryAsync(CacheEntry entry, object id, IEntityPersister persister, LoadEvent @event, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			object optionalObject = @event.InstanceToLoad;
			IEventSource session = @event.Session;
			ISessionFactoryImplementor factory = session.Factory;

			if (log.IsDebugEnabled())
			{
				log.Debug("assembling entity from second-level cache: {0}", MessageHelper.InfoString(persister, id, factory));
			}

			IEntityPersister subclassPersister = factory.GetEntityPersister(entry.Subclass);
			object result = optionalObject ?? session.Instantiate(subclassPersister, id);

			// make it circular-reference safe
			EntityKey entityKey = session.GenerateEntityKey(id, subclassPersister);
			TwoPhaseLoad.AddUninitializedCachedEntity(entityKey, result, subclassPersister, LockMode.None, entry.Version, session);

			IType[] types = subclassPersister.PropertyTypes;
			object[] values = await (entry.AssembleAsync(result, id, subclassPersister, session.Interceptor, session, cancellationToken)).ConfigureAwait(false); // intializes result by side-effect
			TypeHelper.DeepCopy(values, types, subclassPersister.PropertyUpdateability, values, session);

			object version = Versioning.GetVersion(values, subclassPersister);
			if (log.IsDebugEnabled())
			{
				log.Debug("Cached Version: {0}", version);
			}

			IPersistenceContext persistenceContext = session.PersistenceContext;
			bool isReadOnly = session.DefaultReadOnly;

			if (persister.IsMutable)
			{
				object proxy = persistenceContext.GetProxy(entityKey);
				if (proxy != null)
				{
					// this is already a proxy for this impl
					// only set the status to read-only if the proxy is read-only
					isReadOnly = ((INHibernateProxy)proxy).HibernateLazyInitializer.ReadOnly;
				}
			}
			else
				isReadOnly = true;
			
			persistenceContext.AddEntry(
				result,
				isReadOnly ? Status.ReadOnly : Status.Loaded,
				values,
				null,
				id,
				version,
				LockMode.None,
				true,
				subclassPersister,
				false);
			
			subclassPersister.AfterInitialize(result, session);
			await (persistenceContext.InitializeNonLazyCollectionsAsync(cancellationToken)).ConfigureAwait(false);
			// upgrade the lock if necessary:
			//lock(result, lockMode);

			//PostLoad is needed for EJB3
			//TODO: reuse the PostLoadEvent...
			PostLoadEvent postLoadEvent = new PostLoadEvent(session);
			postLoadEvent.Entity = result;
			postLoadEvent.Id = id;
			postLoadEvent.Persister = persister;

			IPostLoadEventListener[] listeners = session.Listeners.PostLoadEventListeners;
			for (int i = 0; i < listeners.Length; i++)
			{
				listeners[i].OnPostLoad(postLoadEvent);
			}
			return result;
		}
	}
}
