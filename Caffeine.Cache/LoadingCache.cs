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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Caffeine.Cache
{
    /// <summary>
    /// This class provides a skeletal implementation of the <see cref="ILoadingCache{K, V}"/> interface to minimize
    /// the effort required to implement a <see cref="LocalCache{K, V}"/>.
    /// </summary>
    /// <typeparam name="C"></typeparam>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public abstract class LoadingCache<C, K, V> : ManualCache<C, K, V>, ILoadingCache<K, V> where C : LocalCache<K, V>
    {
        public LoadingCache(C cache)
            : base()
        {
            this.cache = cache;
        }

        internal virtual CacheLoader<K, V> CacheLoader
        {
            get;
        }

        internal virtual Func<K, V> MappingFunction
        {
            get;
        }

        internal abstract bool HasBulkLoader { get; }

        internal bool HasLoadAll(CacheLoader<K, V> loader)
        {
            return loader.HasBulkLoader;
        }

        public IReadOnlyDictionary<K, V> GetAll(IEnumerable<K> keys)
        {
            return HasBulkLoader ? LoadInBulk(keys) : LoadSequentially(keys);
        }

        public V GetOrAdd(K key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sequentially load each missing entry.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        internal IReadOnlyDictionary<K, V> LoadSequentially(IEnumerable<K> keys)
        {
            HashSet<K> uniqueKeys = new HashSet<K>(keys);

            int count = 0;
            Dictionary<K, V> result = new Dictionary<K, V>(uniqueKeys.Count);
            try
            {
                foreach (K key in uniqueKeys)
                {
                    count++;

                    V value = TryGetValue(key);
                    if (!EqualityComparer<V>.Default.Equals(value, default(V)))
                        result.Add(key, value);
                }
            }
            catch (Exception e)
            {
                cache.StatsCounter.RecordMisses(uniqueKeys.Count - count);
                throw;
            }

            return result;
        }

        /// <summary>
        /// Batch loads the missing entries.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        internal IReadOnlyDictionary<K, V> LoadInBulk(IEnumerable<K> keys)
        {
            // TODO: implement the GetAllPresent
            //Dictionary<K, V> found = cache.GetAllPresent(keys);
            Dictionary<K, V> found = new Dictionary<K, V>();
            HashSet<K> keysToLoad = new HashSet<K>();
            foreach (K key in keys)
            {
                if (!found.ContainsKey(key))
                    keysToLoad.Add(key);
            }

            if (keysToLoad.Count == 0)
                return found;

            Dictionary<K, V> result = new Dictionary<K, V>();
            BulkLoad(keysToLoad, result);
            return result;
        }

        /// <summary>
        /// Performs a non-blocking bulk load of the missing keys. Any missing entry that materializes
        /// during the load are replaced when the loaded entries are inserted into the cache.
        /// </summary>
        /// <param name="keysToLoad"></param>
        /// <param name="result"></param>
        internal void BulkLoad(HashSet<K> keysToLoad, Dictionary<K, V> result)
        {
            bool success = false;
            long startTime = cache.StatsTicker.Ticks();

            try
            {
                CacheBulkLoader<K, V> loader = CacheLoader as CacheBulkLoader<K, V>;
                Dictionary<K, V> loaded = loader.LoadAll(keysToLoad);
                foreach (KeyValuePair<K, V> kvp in loaded)
                {
                    cache.Add(kvp.Key, kvp.Value, false);
                }

                foreach (K key in keysToLoad)
                {
                    V value = default(V);
                    if (loaded.TryGetValue(key, out value))
                    {
                        if (!EqualityComparer<V>.Default.Equals(value, default(V)))
                        {
                            result.Add(key, value);
                        }
                    }
                }

                success = loaded.Count != 0;
            }
            finally
            {
                long loadTime = cache.StatsTicker.Ticks() - startTime;
                if (success)
                    cache.StatsCounter.RecordLoadSuccess(loadTime);
                else
                    cache.StatsCounter.RecordLoadFailure(loadTime);
            }
        }

        public void Refresh(K key)
        {
            RequireNonNull<K>(key);

            long writeTime = 0L;
            long startTIme = cache.StatsTicker.Ticks();
            V oldValue = cache.TryGetValueQuietly(key, ref writeTime);

            Task<V> refresh;
            if (EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                refresh = CacheLoader.LoadAsync(key);
            else
                refresh = CacheLoader.ReloadAsync(key, oldValue);

            refresh.ContinueWith<V>((t) =>
            {
                long loadTime = cache.StatsTicker.Ticks() - startTIme;

                if (t.IsFaulted)
                {
                    cache.StatsCounter.RecordLoadFailure(loadTime);
                    return default(V);
                }

                V newValue = t.Result;

                bool discard = false;
                // TODO: add this implementation.
                //cache.AddOrUpdate(key, );

                if (discard && cache.HasRemovalListener)
                    cache.NotifyRemoval(key, newValue, RemovalCause.REPLACED);

                if (EqualityComparer<V>.Default.Equals(newValue, default(V)))
                    cache.StatsCounter.RecordLoadFailure(loadTime);
                else
                    cache.StatsCounter.RecordLoadSuccess(loadTime);

                return newValue;
            });


        }

        private void RequireNonNull<V1>(V1 item)
        {
            if (EqualityComparer<V1>.Default.Equals(item, default(V1)))
                throw new ArgumentNullException("item", "item cannot be null.");
        }
    }
}
