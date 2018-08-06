/*
 * Copyright 2018 Randy Lynn, Zach Jones. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the Liense at
 * 
 *       http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Original Author in JAVA: Ben Manes (ben.manes@gmail.com)
 * Ported to C# .NET by: Randy Lynn (randy.lynn.klex@gmail.com), Zach Jones (zachary.b.jones@gmail.com)
 * 
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache
{
    public abstract class LocalCache<K, V> : ConcurrentDictionary<K, V>
    {
        private const bool RECORD_LOAD = true;
        private const bool RECORD_MISS = true;

        /// <summary>
        /// Returns whether this cache has statistics enabled.
        /// </summary>
        internal abstract bool IsRecordingStats { get; }

        /// <summary>
        /// Returns the <see cref="IStatsCounter"/> used by this cache.
        /// </summary>
        public abstract IStatsCounter StatsCounter { get; }

        /// <summary>
        /// Returns whether this cache notifies when an entry is removed.
        /// </summary>
        internal bool HasRemovalListener
        {
            get { return RemovalListener != null; }
        }

        // TODO: Randy Lynn 2018-07-14 - I think RemovalListener and NotifyRemoval could/should be implemented as events in .NET???
        /// <summary>
        /// Returns the <see cref="IRemovalListener{K, V}"/> used by this cache.
        /// </summary>
        public IRemovalListener<K, V> RemovalListener { get; protected set; }

        /// <summary>
        /// Asynchronously sneds a removal notification to the listener.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="cause"></param>
        internal virtual void NotifyRemoval(K key, V value, RemovalCause cause)
        {
            Task.Run(() => RemovalListener.OnRemoval(key, value, cause));
        }

        /// <summary>
        /// Returns whether the cache captures the write time of the entry.
        /// </summary>
        public abstract bool HasWriteTime { get; }

        /// <summary>
        /// Returns the <see cref="ITicker"/> used by this cache for expiration.
        /// </summary>
        internal abstract ITicker ExpirationTicker { get; }

        /// <summary>
        /// Returns the <see cref="ITicker"/> used by this cache for statistics
        /// </summary>
        internal abstract ITicker StatsTicker { get; }

        /// <summary>
        /// <see cref="ICache{K, V}.EstimatedSize"/>
        /// </summary>
        public abstract long EstimatedSize();

        /// <summary>
        /// <seealso cref="ICache{K, V}.TryGetValue(K)"/> this method differs by accepting a parameter of 
        /// whether to record the hit and miss statistics on the success of this operation.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="recordStats"></param>
        /// <returns></returns>
        public abstract V TryGetValue(K key, bool recordStats);

        /// <summary>
        /// <seealso cref="ICache{K, V}.TryGetValue(K)"/>. This method differs by not recording the access
        /// with the statistics nor the eviction policy, and populates the write time if known.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="writeTime"></param>
        /// <returns></returns>
        public abstract V TryGetValueQuietly(K key, ref long writeTime);

        /// <summary>
        /// <seealso cref="ICache{K, V}.TryGetAll(IEnumerable{K})"/>
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public abstract Dictionary<K, V> TryGetAll(IEnumerable<K> keys);

        public virtual V GetOrAdd(K key, Func<K, V, V> mappingFunction)
        {
            return this.GetOrAdd(key, mappingFunction, true, true);
        }

        public abstract V GetOrAdd(K key, Func<K, V, V> mappingFunction, bool recordStats, bool recordLoad);

        public virtual V Compute(K key, Func<K, V, V> remappingFunction)
        {
            return this.Compute(key, remappingFunction, false, false);
        }

        public abstract V Compute(K key, Func<K, V, V> remappingFunction, bool recordMiss, bool recordLoad);


        /// <summary>
        /// See <seealso cref="ICache{K, V}.InvalidateAll(IEnumerable{K})"/>.
        /// </summary>
        /// <param name="keys"></param>
        public virtual void InvalidateAll(IEnumerable<K> keys)
        {
            foreach (K key in keys)
            {
                V val;
                base.TryRemove(key, out val);
            }
        }

        /// <summary>
        /// <seealso cref="ICache{K, V}.CleanUp"/>
        /// </summary>
        public abstract void CleanUp();

        /// <summary>
        /// <seealso cref="ICache{K, V}.Add(K, V)"/>. This method differs by allowing the operation to not notify
        /// the writer when an entry was inserted or updated.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="notifyWriter"></param>
        /// <returns></returns>
        public abstract V Add(K key, V value, bool notifyWriter);

        #region ConcurrentDictionary language

        public new abstract int Count { get; }

        public new abstract bool IsEmpty { get; }

        public new virtual ICollection<K> Keys { get { throw new NotImplementedException(); } }

        public new virtual ICollection<V> Values { get { throw new NotImplementedException(); } }


        public new virtual V AddOrUpdate<TArg>(K key, Func<K, TArg, V> addValueFactory, Func<K, V, TArg, V> updateValueFactory, TArg factoryArgument)
        {
            throw new NotImplementedException();
        }

        public new virtual V AddOrUpdate(K key, Func<K, V> addValueFactory, Func<K, V, V> updateValueFactory)
        {
            throw new NotImplementedException();
        }

        public new virtual V AddOrUpdate(K key, V addValue, Func<K, V, V> updateValueFactory)
        {
            throw new NotImplementedException();
        }


        public new abstract void Clear();

        /// <summary>
        /// Determines whether the cache contains the specified <paramref name="key"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public new abstract bool ContainsKey(K key);

        public new virtual V GetOrAdd(K key, V value)
        {
            throw new NotImplementedException();
        }

        public new virtual V GetOrAdd(K key, Func<K, V> valueFactory)
        {
            throw new NotImplementedException();
        }

        public new virtual V GetOrAdd<TArg>(K key, Func<K, TArg, V> valueFactory, TArg factoryArgument)
        {
            throw new NotImplementedException();
        }

        public new abstract bool TryAdd(K key, V value);

        public new abstract bool TryGetValue(K key, out V value);

        public new abstract bool TryRemove(K key, out V value);

        public new abstract bool Remove(K key);

        public new virtual bool TryUpdate(K key, V newValue, V comparisonValue)
        {
            throw new NotImplementedException();
        }
        #endregion

        /// <summary>
        /// Decorates the remapping function to record statistics if enabled.
        /// </summary>
        /// <param name="mappingFunction"></param>
        /// <param name="shouldRecordLoad"></param>
        /// <returns></returns>
        protected virtual Func<K, V> StatsAware(Func<K, V> mappingFunction, bool shouldRecordLoad)
        {
            if (!IsRecordingStats)
                return mappingFunction;

            return (k) =>
            {
                V value;

                StatsCounter.RecordMisses(1);
                long startTime = StatsTicker.Ticks();
                try
                {
                    value = mappingFunction(k);
                }
                catch (Exception e)
                {
                    StatsCounter.RecordLoadFailure(StatsTicker.Ticks() - startTime);
                    throw;
                }

                long loadTime = StatsTicker.Ticks() - startTime;

                if (shouldRecordLoad)
                {
                    if (value == null)
                        StatsCounter.RecordLoadFailure(loadTime);
                    else
                        StatsCounter.RecordLoadSuccess(loadTime);
                }

                return value;
            };
        }

        /// <summary>
        /// Decorates the remapping function to record statistics if enabled.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <param name="remappingFunction"></param>
        /// <returns></returns>
        protected Func<T1, T2, TR> StatsAware<T1, T2, TR>(Func<T1, T2, TR> remappingFunction)
        {
            return StatsAware(remappingFunction, RECORD_MISS, RECORD_LOAD);
        }

        /// <summary>
        /// Decorates the remapping function to record statistics if enabled
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <param name="remappingFunction"></param>
        /// <param name="shouldRecordMiss"></param>
        /// <param name="shouldRecordLoad"></param>
        /// <returns></returns>
        protected Func<T1, T2, TR> StatsAware<T1, T2, TR>(Func<T1, T2, TR> remappingFunction, bool shouldRecordMiss, bool shouldRecordLoad)
        {
            if (!IsRecordingStats)
                return remappingFunction;

            return (t, u) =>
            {
                TR result;

                if ((u == null) && shouldRecordMiss)
                    StatsCounter.RecordMisses(1);

                long startTime = StatsTicker.Ticks();

                try
                {
                    result = remappingFunction(t, u);
                }
                catch (Exception e)
                {
                    StatsCounter.RecordLoadFailure(StatsTicker.Ticks() - startTime);
                    throw;
                }

                long loadTime = StatsTicker.Ticks() - startTime;

                if (shouldRecordLoad)
                {
                    if (result == null)
                        StatsCounter.RecordLoadFailure(loadTime);
                    else
                        StatsCounter.RecordLoadSuccess(loadTime);
                }

                return result;
            };
        }
    }
}
