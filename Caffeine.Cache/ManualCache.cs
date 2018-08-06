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

using Caffeine.Cache.Stats;

namespace Caffeine.Cache
{
    public abstract class ManualCache<C, K, V> : ICache<K, V> where C : LocalCache<K, V>
    {
        protected C cache;

        public ManualCache()
        { }

        public abstract IPolicy<K, V> Policy { get; }

        public CacheStats Stats
        {
            get { return cache.StatsCounter.Snapshot(); }
        }

        public long EstimatedSize()
        {
            return cache.EstimatedSize();
        }

        public void CleanUp()
        {
            cache.CleanUp();
        }

        public V TryGetValue(K key)
        {
            return cache.TryGetValue(key, true);
        }

        public void Invalidate(K key)
        {
            cache.TryRemove(key, out V val);
        }

        public void InvalidateAll(IEnumerable<K> keys)
        {
            foreach (K key in keys)
            {
                cache.TryRemove(key, out V val);
            }
        }

        public void InvalidateAll()
        {
            cache.Clear();
        }

        public ConcurrentDictionary<K, V> AsConcurrentDictionary()
        {
            return cache;
        }

        public V GetOrAdd(K key, Func<K, V> mappingFunction)
        {
            return cache.GetOrAdd(key, mappingFunction);
        }

        public Dictionary<K, V> TryGetAll(IEnumerable<K> keys)
        {
            return cache.TryGetAll(keys);
        }

        public void Add(K key, V value)
        {
            cache.TryAdd(key, value);
        }

        public void Add(Dictionary<K, V> map)
        {
            foreach (KeyValuePair<K, V> kvp in map)
            {
                cache.TryAdd(kvp.Key, kvp.Value);
            }
        }

        public void Clear()
        {
            cache.Clear();
        }
    }
}
