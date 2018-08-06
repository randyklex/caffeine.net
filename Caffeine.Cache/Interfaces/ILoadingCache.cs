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

using System.Collections.Generic;

namespace Caffeine.Cache
{
    public interface ILoadingCache<K, V> : ICache<K, V>
    {
        /// <summary>
        /// Returns the value associated with the <paramref name="key"/> in this cache, obtaining the value from
        /// <see cref="CacheLoader{K, V}.Load(K)"/> if necessary.
        /// 
        /// If another call to <see cref="GetOrAdd(K)"/> is currently loading the value for the <paramref name="key"/>, this
        /// thread simply waits for that thread to finish and returns its loaded value. Multiple threads can concurrently
        /// load values for distinct keys.
        /// 
        /// If the specified <paramref name="key"/> is not already associated with a value, attempts to compute its value
        /// and enters it into this cache unless <see langword="null"/>. The entire method invocation is performed
        /// atomically, so the function is aplpied at most, once per key. Some attempted update operations on this cache
        /// by other threads may be blocked while the computation is in progress, so the computation should be short 
        /// and simple, and must not attempt to update any other mappings of this cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        V GetOrAdd(K key);

        /// <summary>
        /// Returns a <see cref="IReadOnlyDictionary{TKey, TValue}"/> of the values associated with the <paramref name="keys"/>, creating or 
        /// retrieving those values if necessary. The returned <see cref="IReadOnlyDictionary{TKey, TValue}"/> contains entries that were 
        /// already cached, combined with the newly loaded entries; it will never contain null keys or values.
        /// 
        /// Caches loaded by a <see cref="CacheLoader{K, V}"/> will issue a single request to <see cref="CacheLoader{K, V}.LoadAll(IEnumerable{K})"/>
        /// for all keys which are not already present in the cache. All entries returned by <see cref="CacheLoader{K, V}.LoadAll(IEnumerable{K})"/>
        /// will be stored in the cache, over-writing any previously cached values. If another call to <see cref="GetOrAdd(K)"/> tries to load
        /// the value for a key in <paramref name="keys"/>, implementation may either have that thread load the entry or simply wait
        /// for this thread to finish and returns the loaded value. In the case of overlapping non-blocking loads, the last load
        /// to complete will replace the existing entry. Note that multiple threads can conrurently load values for distinct keys.
        /// 
        /// Duplicate keys in <paramref name="keys"/> will be ignored.
        /// </summary>
        /// <param name="keys">keys whose associated values are to be returned.</param>
        /// <returns></returns>
        IReadOnlyDictionary<K, V> GetAll(IEnumerable<K> keys);

        //TODO: ZBJ- 2018/05/10 The java library says that this loads a new value for the given key asynchronously.  That definitely makes sense in the IAsyncLoadingCache, but I would think we would
        // want to load it synchronously in this one.
        void Refresh(K key);
    }
}
