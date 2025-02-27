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
using NHibernate.Engine;
using NHibernate.Persister.Entity;
using NHibernate.Type;
using NHibernate.Util;

namespace NHibernate.Loader.Entity
{
	using System.Threading.Tasks;
	using System.Threading;
	public partial class BatchingEntityLoader : AbstractBatchingEntityLoader
	{

		public override async Task<object> LoadAsync(object id, object optionalObject, ISessionImplementor session, bool checkCache, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			object[] batch =
				await (session.PersistenceContext.BatchFetchQueue.GetEntityBatchAsync(Persister, id, batchSizes[0], cancellationToken)).ConfigureAwait(false);

			for (int i = 0; i < batchSizes.Length - 1; i++)
			{
				int smallBatchSize = batchSizes[i];
				if (batch[smallBatchSize - 1] != null)
				{
					object[] smallBatch = new object[smallBatchSize];
					Array.Copy(batch, 0, smallBatch, 0, smallBatchSize);

					IList results =
						await (loaders[i].LoadEntityBatchAsync(session, smallBatch, idType, optionalObject, Persister.EntityName, id, Persister, cancellationToken)).ConfigureAwait(false);

					return GetObjectFromList(results, id, session); //EARLY EXIT
				}
			}

			return await (((IUniqueEntityLoader) loaders[batchSizes.Length - 1]).LoadAsync(id, optionalObject, session, checkCache, cancellationToken)).ConfigureAwait(false);
		}
	}
}
