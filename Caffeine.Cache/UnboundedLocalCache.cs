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

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache
{
    /// <summary>
    /// An in-memory cache that has no capabilities for bounding the dictionary. This implementation
    /// provides a lightweight wrapper on top of <see cref="ConcurrentDictionary{TKey, TValue}"/>
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public sealed class UnboundedLocalCache<K, V> : LocalCache<K, V>
    {
        private static readonly int NUM_CPUS = Environment.ProcessorCount;

        readonly IRemovalListener<K, V> removalListener;
        private readonly ConcurrentDictionary<K, V> data;
        private readonly IStatsCounter statsCounter;
        private readonly ICacheWriter<K, V> writer;
        private readonly ITicker ticker;

        private HashSet<K> keySet;
        private ICollection<V> values;
        private HashSet<KeyValuePair<K, V>> entries;

        private readonly bool isRecordingStats;

        public UnboundedLocalCache(Caffeine<K, V> builder, bool isAsync)
            : this(builder, isAsync, NUM_CPUS + 2)
        { }

        public UnboundedLocalCache(Caffeine<K, V> builder, bool isAsync, int concurrencyLevel)
        {
            this.data = new ConcurrentDictionary<K, V>(concurrencyLevel, builder.InitialCapacity);
            this.statsCounter = builder.StatsCounter.Get();
            this.removalListener = builder.RemovalListener;
            this.isRecordingStats = builder.IsRecordingStats;
            this.writer = builder.Writer;
            this.ticker = builder.Ticker;
        }

        public override bool HasWriteTime
        {
            get { return false; }
        }

        public V TryGetValue(K key)
        {
            long tmpVal = 0L;
            return TryGetValueQuietly(key, ref tmpVal);
        }

        public override bool TryGetValue(K key, out V value)
        {
            long tmpVal = 0L;
            V transient = TryGetValueQuietly(key, ref tmpVal);

            bool rval = false;
            value = transient;
            if (!EqualityComparer<V>.Default.Equals(transient, default(V)))
                rval = true;

            return rval;
        }

        public override V TryGetValue(K key, bool recordStats)
        {
            V val;
            data.TryGetValue(key, out val);

            if (recordStats)
            {
                if (val == null)
                    statsCounter.RecordMisses(1);
                else
                    statsCounter.RecordHits(1);
            }

            return val;
        }

        public override V TryGetValueQuietly(K key, ref long writeTime)
        {
            V val;
            data.TryGetValue(key, out val);

            return val;
        }

        public override long EstimatedSize()
        {
            return data.Count;
        }

        public override Dictionary<K, V> TryGetAll(IEnumerable<K> keys)
        {
            HashSet<K> uniqueKeys = new HashSet<K>();
            foreach (K key in keys)
            {
                uniqueKeys.Add(key);
            }

            int misses = 0;
            Dictionary<K, V> result = new Dictionary<K, V>(uniqueKeys.Count);
            foreach (K key in uniqueKeys)
            {
                V val;
                if (data.TryGetValue(key, out val))
                {
                    result.Add(key, val);
                }
                else
                    misses++;
            }

            statsCounter.RecordMisses(misses);
            statsCounter.RecordHits(result.Count);

            return result;
        }


        public override void CleanUp()
        { }

        public override IStatsCounter StatsCounter
        {
            get { return statsCounter; }
        }

        internal override bool IsRecordingStats
        {
            get { return isRecordingStats; }
        }

        internal override ITicker ExpirationTicker
        {
            get { return DisabledTicker.Instance; }
        }

        internal override ITicker StatsTicker
        {
            get { return ticker; }
        }

        internal V Remap(K key, Func<K, V, V> remappingFunction)
        {
            V oldValue = default(V);
            RemovalCause cause = RemovalCause.UNKNOWN;

            V nv = data.AddOrUpdate(key, delegate (K k1)
            {
                V newValue = remappingFunction(k1, default(V));
                return newValue;
            },
            delegate (K k1, V v1)
            {
                V newValue = remappingFunction(k1, v1);

                cause = (newValue == null) ? RemovalCause.EXPLICIT : RemovalCause.REPLACED;

                if (HasRemovalListener && !EqualityComparer<V>.Default.Equals(v1, default(V)) && !EqualityComparer<V>.Default.Equals(newValue, v1))
                    oldValue = v1;

                return newValue;
            });

            if (!EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                NotifyRemoval(key, oldValue, cause);

            return nv;
        }

        #region Concurrent Dictionary

        public override bool IsEmpty
        {
            get { return data.IsEmpty; }
        }

        public override int Count
        {
            get { return data.Count; }
        }

        public override void Clear()
        {
            if (!HasRemovalListener && writer == DisabledCacheWriter<K, V>.Instance)
            {
                data.Clear();
                return;
            }

            foreach (K key in data.Keys)
            {
                V oldVal = default(V);
                TryRemove(key, out oldVal);
            }
        }

        public override bool ContainsKey(K key)
        {
            return data.ContainsKey(key);
        }

        // TODO: Randy Lynn 2018-07-21 did not implement ContainsValue... 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public V AddOrUpdate(K key, V value)
        {
            return AddOrUpdate(key, value, true);
        }

        // TODO: this was put(K key, V value, bool notifyWriter);
        public V AddOrUpdate(K key, V value, bool notifyWriter)
        {
            RequireNonNull<V>(value);

            V oldValue = default(V);

            if ((writer == DisabledCacheWriter<K, V>.Instance) || !notifyWriter)
            {
                if (data.TryAdd(key, value))
                    oldValue = value;
            }
            else
            {
                data.AddOrUpdate(key, value, (k, v) =>
                {
                    if (!EqualityComparer<V>.Default.Equals(value, v))
                        writer.Write(key, value);

                    oldValue = v;
                    return value;
                });
            }

            if (HasRemovalListener)
            {
                if (!EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                {
                    if (!EqualityComparer<V>.Default.Equals(oldValue, value))
                    {
                        NotifyRemoval(key, oldValue, RemovalCause.REPLACED);
                    }
                }
            }

            return oldValue;
        }

        public override bool TryAdd(K key, V value)
        {
            RequireNonNull<V>(value);

            bool added = data.TryAdd(key, value);

            if (added)
                writer.Write(key, value);

            return added;
        }

        public void PutAll(Dictionary<K, V> map)
        {
            foreach (KeyValuePair<K, V> kvp in map)
            {
                TryAdd(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Atttempts to remove the <paramref name="key"/> from the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true if it was in the cache. false if the key was not present.</returns>
        public override bool Remove(K key)
        {
            V oldValue = default(V);
            return TryRemove(key, out oldValue);
        }

        /// <summary>
        /// Atttempts to remove the <paramref name="key"/> from the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if the <paramref name="key"/> was in the cache. false if the key was not present.</returns>
        public override bool TryRemove(K key, out V oldValue)
        {
            if (!data.TryRemove(key, out oldValue))
                return false;

            if (writer != DisabledCacheWriter<K, V>.Instance)
                writer.Delete(key, oldValue, RemovalCause.EXPLICIT);

            if (HasRemovalListener && !EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                NotifyRemoval(key, oldValue, RemovalCause.EXPLICIT);

            return true;
        }

        /// <summary>
        /// Attempts to remove the <paramref name="key"/> with the associated <paramref name="value"/>. If the 
        /// <paramref name="value"/> does not match what is currently associated with the <paramref name="key"/>
        /// then the mapping is not removed from the cache.
        /// </summary>
        /// <param name="key">A key to remove</param>
        /// <param name="value">the value you expect to be associated with the key.</param>
        /// <returns>true if the item was removed from the cache, otherwise false.</returns>
        /// <exception cref="ArgumentNullException">If the <paramref name="value"/> is <see langword="null"/>, and the <paramref name="key"/> is <see langword="null"/></exception>
        public bool TryRemove(K key, V value)
        {
            if (EqualityComparer<V>.Default.Equals(value, default(V)))
                RequireNonNull<K>(key);

            bool rval = false;

            if (data.TryGetValue(key, out V existingVal))
            {
                if (EqualityComparer<V>.Default.Equals(value, existingVal))
                {
                    rval = true;
                    TryRemove(key, out existingVal);
                }
            }

            return rval;
        }

        /// <summary>
        /// Will replace whatever value is currently associated with the <paramref name="key"/>. If the <paramref name="key"/> is not
        /// already present, then will create the entry and map the <paramref name="value"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>if the <paramref name="key"/> already exist, returns the previously mapped value.</returns>
        /// <exception cref="ArgumentNullException">if the <paramref name="value"/> is <see langword="null"/></exception>
        public V Replace(K key, V value)
        {
            RequireNonNull<V>(value);

            V oldValue = default(V);

            data.AddOrUpdate(key, value, (k, v) =>
            {
                if (!EqualityComparer<V>.Default.Equals(value, v))
                    writer.Write(key, value);

                oldValue = v;
                return value;
            });

            if (HasRemovalListener)
            {
                if (!EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                {
                    if (!EqualityComparer<V>.Default.Equals(oldValue, value))
                    {
                        NotifyRemoval(key, oldValue, RemovalCause.REPLACED);
                    }
                }
            }

            return oldValue;
        }

        /// <summary>
        /// Replaces the value associated with the <paramref name="key"/> if and only if, the mapped value
        /// for the <paramref name="key"/> is equal to <paramref name="oldValue"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If the <paramref name="oldValue"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException">If the <paramref name="newValue"/> is <see langword="null"/></exception>
        public bool Replace(K key, V oldValue, V newValue)
        {
            RequireNonNull<V>(oldValue);
            RequireNonNull<V>(newValue);

            bool updated = data.TryUpdate(key, newValue, oldValue);

            if (updated && HasRemovalListener)
            {
                if (!EqualityComparer<V>.Default.Equals(newValue, oldValue))
                    NotifyRemoval(key, oldValue, RemovalCause.REPLACED);
            }

            return updated;
        }

        public override bool Equals(object obj)
        {
            return data.Equals(obj);
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }

        #endregion

        private void RequireNonNull<V1>(V1 val)
        {
            if (EqualityComparer<V1>.Default.Equals(val, default(V1)))
                throw new ArgumentNullException("val", "value cannot be NULL.");
        }

        public override V GetOrAdd(K key, Func<K, V, V> mappingFunction, bool recordStats, bool recordLoad)
        {
            throw new NotImplementedException();
        }

        public override V Compute(K key, Func<K, V, V> remappingFunction, bool recordMiss, bool recordLoad)
        {
            throw new NotImplementedException();
        }

        public override V Add(K key, V value, bool notifyWriter)
        {
            throw new NotImplementedException();
        }
    }
}
