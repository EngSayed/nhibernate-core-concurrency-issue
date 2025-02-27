﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by AsyncGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using NHibernate.Cache;
using NHibernate.Collection;
using NHibernate.Criterion;
using NHibernate.Engine;
using NHibernate.Engine.Query;
using NHibernate.Engine.Query.Sql;
using NHibernate.Event;
using NHibernate.Hql;
using NHibernate.Id;
using NHibernate.Loader.Criteria;
using NHibernate.Loader.Custom;
using NHibernate.Persister.Entity;
using NHibernate.Proxy;
using NHibernate.Type;
using NHibernate.Util;

namespace NHibernate.Impl
{
	using System.Threading.Tasks;
	using System.Threading;
	public partial class StatelessSessionImpl : AbstractSessionImpl, IStatelessSession
	{

		public override Task InitializeCollectionAsync(IPersistentCollection collection, bool writing, CancellationToken cancellationToken)
		{
			if (temporaryPersistenceContext.IsLoadFinished)
			{
				throw new SessionException("Collections cannot be fetched by a stateless session. You can eager load it through specific query.");
			}
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			try
			{
				CollectionEntry ce = temporaryPersistenceContext.GetCollectionEntry(collection);
				if (!collection.WasInitialized)
				{
					return ce.LoadedPersister.InitializeAsync(ce.LoadedKey, this, cancellationToken);
				}
				return Task.CompletedTask;
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		public override async Task<object> InternalLoadAsync(string entityName, object id, bool eager, bool isNullable, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				IEntityPersister persister = Factory.GetEntityPersister(entityName);
				object loaded = temporaryPersistenceContext.GetEntity(GenerateEntityKey(id, persister));
				if (loaded != null)
				{
					return loaded;
				}
				if (!eager && persister.HasProxy)
				{
					return persister.CreateProxy(id, this);
				}
				//TODO: if not loaded, throw an exception
				return await (GetAsync(entityName, id, cancellationToken)).ConfigureAwait(false);
			}
		}

		public override Task<object> ImmediateLoadAsync(string entityName, object id, CancellationToken cancellationToken)
		{
			throw new SessionException("proxies cannot be fetched by a stateless session");
		}

		public override Task<IQuery> CreateFilterAsync(object collection, IQueryExpression queryExpression, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override async Task ListAsync(IQueryExpression queryExpression, QueryParameters queryParameters, IList results, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				// We need to flush the batcher. Otherwise it may have pending operations which will not already have reached the database,
				// and the query may yield stale data.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);

				queryParameters.ValidateParameters();
				var plan = GetHQLQueryPlan(queryExpression, false);

				bool success = false;
				try
				{
					await (plan.PerformListAsync(queryParameters, this, results, cancellationToken)).ConfigureAwait(false);
					success = true;
				}
				catch (OperationCanceledException) { throw; }
				catch (HibernateException)
				{
					// Do not call Convert on HibernateExceptions
					throw;
				}
				catch (Exception e)
				{
					throw Convert(e, "Could not execute query");
				}
				finally
				{
					await (AfterOperationAsync(success, cancellationToken)).ConfigureAwait(false);
				}
				temporaryPersistenceContext.Clear();
			}
		}

		public override async Task<IList<T>> ListAsync<T>(CriteriaImpl criteria, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				// We need to flush the batcher. Otherwise it may have pending operations which will not already have reached the database,
				// and the query may yield stale data.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);

				string[] implementors = Factory.GetImplementors(criteria.EntityOrClassName);
				int size = implementors.Length;

				CriteriaLoader[] loaders = new CriteriaLoader[size];
				for (int i = 0; i < size; i++)
				{
					loaders[size - 1 - i] = new CriteriaLoader(GetOuterJoinLoadable(implementors[i]), Factory,
													criteria, implementors[i], EnabledFilters);
				}

				bool success = false;
				try
				{
					var results = await (loaders.LoadAllToListAsync<T>(this, cancellationToken)).ConfigureAwait(false);
					success = true;
					return results;
				}
				catch (OperationCanceledException) { throw; }
				catch (HibernateException)
				{
					// Do not call Convert on HibernateExceptions
					throw;
				}
				catch (Exception sqle)
				{
					throw Convert(sqle, "Unable to perform find");
				}
				finally
				{
					await (AfterOperationAsync(success, cancellationToken)).ConfigureAwait(false);
					temporaryPersistenceContext.Clear();
				}
			}
		}

		//TODO 6.0: Remove (use base class implementation)
		public override async Task ListAsync(CriteriaImpl criteria, IList results, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ArrayHelper.AddAll(results, await (ListAsync(criteria, cancellationToken)).ConfigureAwait(false));
		}

		public override Task<IEnumerable> EnumerableAsync(IQueryExpression queryExpression, QueryParameters queryParameters, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public override Task<IEnumerable<T>> EnumerableAsync<T>(IQueryExpression queryExpression, QueryParameters queryParameters, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public override Task<IList> ListFilterAsync(object collection, string filter, QueryParameters parameters, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		protected override Task ListFilterAsync(object collection, IQueryExpression queryExpression, QueryParameters parameters, IList results, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<IList<T>> ListFilterAsync<T>(object collection, string filter, QueryParameters parameters, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<IEnumerable> EnumerableFilterAsync(object collection, string filter, QueryParameters parameters, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task<IEnumerable<T>> EnumerableFilterAsync<T>(object collection, string filter, QueryParameters parameters, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public override Task BeforeTransactionCompletionAsync(ITransaction tx, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			var context = TransactionContext;
			if (tx == null && context == null)
				return Task.FromException<object>(new InvalidOperationException("Cannot complete a transaction without neither an explicit transaction nor an ambient one."));
			// Always allow flushing from explicit transactions, otherwise check if flushing from scope is enabled.
			if (tx != null || context.CanFlushOnSystemTransactionCompleted)
				return FlushBeforeTransactionCompletionAsync(cancellationToken);
			return Task.CompletedTask;
		}

		public override Task FlushBeforeTransactionCompletionAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			if (FlushMode != FlushMode.Manual)
				return FlushAsync(cancellationToken);
			return Task.CompletedTask;
		}

		public override Task AfterTransactionCompletionAsync(bool successful, ITransaction tx, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			try
			{
				AfterTransactionCompletion(successful, tx);
				return Task.CompletedTask;
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		public override async Task ListCustomQueryAsync(ICustomQuery customQuery, QueryParameters queryParameters, IList results, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				// We need to flush the batcher. Otherwise it may have pending operations which will not already have reached the database,
				// and the query may yield stale data.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);

				var loader = new CustomLoader(customQuery, Factory);

				var success = false;
				try
				{
					ArrayHelper.AddAll(results, await (loader.ListAsync(this, queryParameters, cancellationToken)).ConfigureAwait(false));
					success = true;
				}
				finally
				{
					await (AfterOperationAsync(success, cancellationToken)).ConfigureAwait(false);
				}
				temporaryPersistenceContext.Clear();
			}
		}

		// Since v5.2
		[Obsolete("This method has no usages and will be removed in a future version")]
		public override Task<IQueryTranslator[]> GetQueriesAsync(IQueryExpression query, bool scalar, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<IQueryTranslator[]>(cancellationToken);
			}
			try
			{
				return Task.FromResult<IQueryTranslator[]>(GetQueries(query, scalar));
			}
			catch (Exception ex)
			{
				return Task.FromException<IQueryTranslator[]>(ex);
			}
		}

		public override Task<object> GetEntityUsingInterceptorAsync(EntityKey key, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			try
			{
				return Task.FromResult<object>(GetEntityUsingInterceptor(key));
			}
			catch (Exception ex)
			{
				return Task.FromException<object>(ex);
			}
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return ManagedFlushAsync(cancellationToken); // NH Different behavior since ADOContext.Context is not implemented
		}

		public async Task ManagedFlushAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				await (Batcher.ExecuteBatchAsync(cancellationToken)).ConfigureAwait(false);
			}
		}

		#region IStatelessSession Members

		public async Task ManagedCloseAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginContext())
			{
				if (IsClosed)
				{
					throw new SessionException("Session was already closed!");
				}
				// We need to flush the batcher. Otherwise it may have pending operations which will never reach the database,
				// although a stateless session is not supposed to retain anything in memory and so should not need any explicit
				// flush from users.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);
				CloseConnectionManager();
				SetClosed();
			}
		}

		/// <summary> Insert a entity.</summary>
		/// <param name="entity">A new transient instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> the identifier of the instance </returns>
		public Task<object> InsertAsync(object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return InsertAsync(null, entity, cancellationToken);
		}

		/// <summary> Insert a row. </summary>
		/// <param name="entityName">The entityName for the entity to be inserted </param>
		/// <param name="entity">a new transient instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		/// <returns> the identifier of the instance </returns>
		public async Task<object> InsertAsync(string entityName, object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				IEntityPersister persister = GetEntityPersister(entityName, entity);
				object id = await (persister.IdentifierGenerator.GenerateAsync(this, entity, cancellationToken)).ConfigureAwait(false);
				object[] state = persister.GetPropertyValues(entity);
				if (persister.IsVersioned)
				{
					object versionValue = state[persister.VersionProperty];
					bool substitute = await (Versioning.SeedVersionAsync(state, persister.VersionProperty, persister.VersionType,
															 persister.IsUnsavedVersion(versionValue), this, cancellationToken)).ConfigureAwait(false);
					if (substitute)
					{
						persister.SetPropertyValues(entity, state);
					}
				}
				if (id == IdentifierGeneratorFactory.PostInsertIndicator)
				{
					id = await (persister.InsertAsync(state, entity, this, cancellationToken)).ConfigureAwait(false);
				}
				else
				{
					await (persister.InsertAsync(id, state, entity, this, cancellationToken)).ConfigureAwait(false);
				}
				persister.SetIdentifier(entity, id);
				return id;
			}
		}

		/// <summary> Update a entity.</summary>
		/// <param name="entity">a detached entity instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public Task UpdateAsync(object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return UpdateAsync(null, entity, cancellationToken);
		}

		/// <summary>Update a entity.</summary>
		/// <param name="entityName">The entityName for the entity to be updated </param>
		/// <param name="entity">a detached entity instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public async Task UpdateAsync(string entityName, object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				IEntityPersister persister = GetEntityPersister(entityName, entity);
				object id = persister.GetIdentifier(entity);
				object[] state = persister.GetPropertyValues(entity);
				object oldVersion;
				if (persister.IsVersioned)
				{
					oldVersion = persister.GetVersion(entity);
					object newVersion = await (Versioning.IncrementAsync(oldVersion, persister.VersionType, this, cancellationToken)).ConfigureAwait(false);
					Versioning.SetVersion(state, newVersion, persister);
					persister.SetPropertyValues(entity, state);
				}
				else
				{
					oldVersion = null;
				}
				await (persister.UpdateAsync(id, state, null, false, null, oldVersion, entity, null, this, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary> Delete a entity. </summary>
		/// <param name="entity">a detached entity instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public Task DeleteAsync(object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return DeleteAsync(null, entity, cancellationToken);
		}

		/// <summary> Delete a entity. </summary>
		/// <param name="entityName">The entityName for the entity to be deleted </param>
		/// <param name="entity">a detached entity instance </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public async Task DeleteAsync(string entityName, object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				IEntityPersister persister = GetEntityPersister(entityName, entity);
				object id = persister.GetIdentifier(entity);
				object version = persister.GetVersion(entity);
				await (persister.DeleteAsync(id, version, entity, this, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary> Retrieve an entity. </summary>
		/// <returns> a detached entity instance </returns>
		public Task<object> GetAsync(string entityName, object id, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return GetAsync(entityName, id, LockMode.None, cancellationToken);
		}

		/// <summary>
		/// Retrieve an entity.
		/// </summary>
		/// <returns> a detached entity instance
		/// </returns>
		public async Task<T> GetAsync<T>(object id, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			return (T) await (GetAsync(typeof(T).FullName, id, cancellationToken)).ConfigureAwait(false);
		}

		/// <summary>
		/// Retrieve an entity, obtaining the specified lock mode.
		/// </summary>
		/// <returns> a detached entity instance </returns>
		public async Task<object> GetAsync(string entityName, object id, LockMode lockMode, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				// We need to flush the batcher. Otherwise it may have pending operations which will not already have reached the database,
				// and the get may miss an entity which should be there.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);

				object result = await (Factory.GetEntityPersister(entityName).LoadAsync(id, null, lockMode ?? LockMode.None, this, true, cancellationToken)).ConfigureAwait(false);
				if (temporaryPersistenceContext.IsLoadFinished)
				{
					temporaryPersistenceContext.Clear();
				}
				return result;
			}
		}

		/// <summary>
		/// Retrieve an entity, obtaining the specified lock mode.
		/// </summary>
		/// <returns> a detached entity instance </returns>
		public async Task<T> GetAsync<T>(object id, LockMode lockMode, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			return (T) await (GetAsync(typeof(T).FullName, id, lockMode, cancellationToken)).ConfigureAwait(false);
		}

		/// <summary>
		/// Refresh the entity instance state from the database.
		/// </summary>
		/// <param name="entity">The entity to be refreshed. </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public async Task RefreshAsync(object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				await (RefreshAsync(BestGuessEntityName(entity), entity, LockMode.None, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Refresh the entity instance state from the database.
		/// </summary>
		/// <param name="entityName">The entityName for the entity to be refreshed. </param>
		/// <param name="entity">The entity to be refreshed.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public Task RefreshAsync(string entityName, object entity, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<object>(cancellationToken);
			}
			return RefreshAsync(entityName, entity, LockMode.None, cancellationToken);
		}

		/// <summary>
		/// Refresh the entity instance state from the database.
		/// </summary>
		/// <param name="entity">The entity to be refreshed. </param>
		/// <param name="lockMode">The LockMode to be applied.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public async Task RefreshAsync(object entity, LockMode lockMode, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				await (RefreshAsync(BestGuessEntityName(entity), entity, lockMode, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Refresh the entity instance state from the database.
		/// </summary>
		/// <param name="entityName">The entityName for the entity to be refreshed. </param>
		/// <param name="entity">The entity to be refreshed. </param>
		/// <param name="lockMode">The LockMode to be applied. </param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the work</param>
		public async Task RefreshAsync(string entityName, object entity, LockMode lockMode, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				// We need to flush the batcher. Otherwise it may have pending operations which will not already have reached the database,
				// and the query may yield stale data.
				await (FlushAsync(cancellationToken)).ConfigureAwait(false);

				IEntityPersister persister = GetEntityPersister(entityName, entity);
				object id = persister.GetIdentifier(entity);
				if (log.IsDebugEnabled())
				{
					log.Debug("refreshing transient {0}", MessageHelper.InfoString(persister, id, Factory));
				}
				//from H3.2 TODO : can this ever happen???
				//		EntityKey key = new EntityKey( id, persister, source.getEntityMode() );
				//		if ( source.getPersistenceContext().getEntry( key ) != null ) {
				//			throw new PersistentObjectException(
				//					"attempted to refresh transient instance when persistent " +
				//					"instance was already associated with the Session: " +
				//					MessageHelper.infoString( persister, id, source.getFactory() )
				//			);
				//		}

				if (persister.HasCache)
				{
					CacheKey ck = GenerateCacheKey(id, persister.IdentifierType, persister.RootEntityName);
					await (persister.Cache.RemoveAsync(ck, cancellationToken)).ConfigureAwait(false);
				}

				string previousFetchProfile = FetchProfile;
				object result;
				try
				{
					FetchProfile = "refresh";
					result = await (persister.LoadAsync(id, entity, lockMode, this, true, cancellationToken)).ConfigureAwait(false);
				}
				finally
				{
					FetchProfile = previousFetchProfile;
				}
				UnresolvableObjectException.ThrowIfNull(result, id, persister.EntityName);
			}
		}

		#endregion

		public override async Task<int> ExecuteNativeUpdateAsync(NativeSQLQuerySpecification nativeSQLQuerySpecification, QueryParameters queryParameters, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				queryParameters.ValidateParameters();
				NativeSQLQueryPlan plan = GetNativeSQLQueryPlan(nativeSQLQuerySpecification);

				bool success = false;
				int result;
				try
				{
					result = await (plan.PerformExecuteUpdateAsync(queryParameters, this, cancellationToken)).ConfigureAwait(false);
					success = true;
				}
				finally
				{
					await (AfterOperationAsync(success, cancellationToken)).ConfigureAwait(false);
				}
				temporaryPersistenceContext.Clear();
				return result;
			}
		}

		public override async Task<int> ExecuteUpdateAsync(IQueryExpression queryExpression, QueryParameters queryParameters, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (BeginProcess())
			{
				queryParameters.ValidateParameters();
				var plan = GetHQLQueryPlan(queryExpression, false);
				bool success = false;
				int result;
				try
				{
					result = await (plan.PerformExecuteUpdateAsync(queryParameters, this, cancellationToken)).ConfigureAwait(false);
					success = true;
				}
				finally
				{
					await (AfterOperationAsync(success, cancellationToken)).ConfigureAwait(false);
				}
				temporaryPersistenceContext.Clear();
				return result;
			}
		}
	}
}
