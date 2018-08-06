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
    /// <summary>
    /// A semi-persistent mapping from keys to values. Cache entries are manually added using
    /// <see cref="ICache{K, V}.GetOrAdd(K, Func{K, V})"/> or <see cref="ICache{K, V}.Add(K, V)"/> and are
    /// stored in the cache until either evicted or manually invalidated.
    /// 
    /// Implementation of this interface are expected to be thread-safe, and can be safely
    /// accessed by multiple concurrent threads.
    /// </summary>
    /// <typeparam name="K">The type of Keys maintained by this cache.</typeparam>
    /// <typeparam name="V">The type of mapped values.</typeparam>
    public interface ICache<K, V>
    {
        /// <summary>
        /// Returns the value associated with the <paramref name="key"/> in this cache, or <code>Default(V)</code>
        /// if there is no cached value for the <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key whos associated value is to be returned.</param>
        /// <returns>The value to which the specified key is mapped, or <code>Default(V)</code> if this <see cref="ICache{K, V}"/> 
        /// contains no mapping for the <paramref name="key"/>.</returns>
        /// <exception cref="ArgumentNullException">If the specified key is null.</exception>
        V TryGetValue(K key);

        /// <summary>
        /// Returns the value associated with the <paramref name="key"/> in this cache, obtaining that
        /// value from the <paramref name="mappingFunction"/> if necessary. This method provides a simple
        /// substitute for the convention "if cached, return; otherwise create, cache and return" pattern.
        /// <para>
        /// If the specified <paramref name="key"/> is not already associated with a value, attempts to 
        /// compute its value using the given mapping function and enters it into this cache unless <see langword="null"/>.
        /// The entire method invocation is performed atomically, so the function is applied at most once per
        /// key. Some attempted update operations on this cache by other threads may be blocked while the
        /// computation is in progress, so the computation should be short and simple, and must not attempt
        /// to update any other mappings of this cache.
        /// </para>
        /// </summary>
        /// <param name="key">The key with which the specified value is to be associated.</param>
        /// <param name="mappingFunction">The function to compute a value.</param>
        /// <returns>The current (existing or computed) value associated with the specified key, or <see langword="null"/>
        /// if computed value is <see langword="null"/></returns>
        /// <exception cref="NullReferenceException">If the specified key or <paramref name="mappingFunction"/> is <see langword="null"/></exception>
        /// <exception cref="InvalidOperationException">If the computation detectably attempts a recursive update to this cache
        /// that would otherwise never compelte.</exception>
        /// <exception cref="SystemException">if the <paramref name="mappingFunction"/> does so, in which case the mapping is left unestablished.</exception>
        V GetOrAdd(K key, Func<K, V> mappingFunction);

        /// <summary>
        /// Returns a <see cref="Dictionary{TKey, TValue}"/> of the values associated with the <paramref name="keys"/> in this cache. The
        /// returned <see cref="Dictionary{TKey, TValue}"/> will only contain entries which are already present
        /// in the cache.
        /// 
        /// NOTE: duplicate elemtns in <paramref name="keys"/> as determined by <see cref="Object.Equals(object)"/>
        /// will be ignored.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        Dictionary<K, V> TryGetAll(IEnumerable<K> keys);

        /// <summary>
        /// Associates the <paramref name="value"/> with the <paramref name="key"/> in this <see cref="ICache{K, V}"/>. If the cache previously
        /// contained a value associated with the <paramref name="key"/>, the old value is replaced by the new <paramref name="value"/>.
        /// Prefer <seealso cref="GetOrAdd(K, Func{K, V})"/> when using the conventional "If cached, return; otherwise create, cache and return" pattern.
        /// </summary>
        /// <param name="key">The key with which the specified value is to be associated</param>
        /// <param name="value">Value to be associated with the specified key.</param>
        /// <exception cref="ArgumentNullException">If the specified <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/></exception>
        void Add(K key, V value);

        /// <summary>
        /// Copies all of the mappings from the specified <paramref name="map"/> to the cache. The effect of this call
        /// is equivalent to that of calling <see cref="Add(K, V)"/> on this cache, once for each mapping
        /// from key <typeparamref name="K"/> to value <typeparamref name="V"/> in the specified <paramref name="map"/>.
        /// The behavior of this operation is undefined if the specified map is modified while the operation is in progress.
        /// </summary>
        /// <param name="map"></param>
        /// <exception cref="ArgumentNullException">if the specified map is <see langword="null"/> or the specified map contains <see langword="null"/> keys or values</exception>
        void Add(Dictionary<K, V> map);

        /// <summary>
        /// Discards all entries in the cache. <seealso cref="ICache{K, V}.InvalidateAll"/>.
        /// <para>
        /// This method was added to normalize terminology consistent with .NET dictionary language.
        /// Clear and InvalidateAll functionally are equivalent. <see cref="ICache{K, V}.InvalidateAll"/> was retained
        /// to be consistent with terminology from the JAVA implementation of Caffeine.
        /// </para>
        /// </summary>
        void Clear();

        /// <summary>
        /// Discards any cached value for the <paramref name="key"/>. The behavior of this operation is undefined for
        /// an entry that is being loaded and is otherwise not present.
        /// </summary>
        /// <param name="key">The key whose mapping is to be removed from the cache.</param>
        /// <exception cref="NullReferenceException">if the specified <paramref name="key"/> is <see langword="null"/></exception>
        void Invalidate(K key);

        /// <summary>
        /// Discards any cached values for the <paramref name="keys"/>. The behavior of this oepration is undefined
        /// for an entry that is being loaded and is otherwise not present.
        /// </summary>
        /// <param name="keys">Keys whose associated values are to be removed.</param>
        /// <exception cref="NullReferenceException">If the specified collection is <see langword="null"/> or contans a <see langword="null"/> element</exception>
        void InvalidateAll(IEnumerable<K> keys);

        /// <summary>
        /// Discards all entries in the cache.
        /// </summary>
        void InvalidateAll();

        /// <summary>
        /// Returns the approximate number of entries in this <see cref="ICache{K, V}"/>. The value returned is an estimate; the
        /// actual count may differ if there are concurrent insertions or removals, or if some entries are
        /// pending removal due to expiration or weak/soft reference collection. In the case of stale entries
        /// this inaccuracy can be mitigated by performing a <see cref="CleanUp"/> first.
        /// </summary>
        /// <returns>The estimated number of mappings</returns>
        long EstimatedSize();

        /// <summary>
        /// Returns a current snapshot of this cache's cumulative statistics. All statistics are
        /// initialized to zero, and are monotonically increasing over the lifetime of the cache.
        /// Due to the performance penalty of maintaining statistics, some implementations many not
        /// record usage history immediately or at all.
        /// </summary>
        CacheStats Stats { get; }

        /// <summary>
        /// Returns a view of the entries stored in this cache as a thread-safe <see cref="ConcurrentDictionary{TKey, TValue}"/>.
        /// Modifications made directly affect the cache.
        /// 
        /// Iterators rfrom the returned <see cref="ConcurrentDictionary{TKey, TValue}"/> are at least weakly consistent: they are
        /// safe for concurrent use, but if the cache is modified (including by eviction) after the iterator is created
        /// it is undefined which of the changes (if any) will be reflected in that iterator.
        /// </summary>
        /// <returns></returns>
        // TODO: I think this is a bad idea exposing the underlying data store.. revisit, and consider removing.
        ConcurrentDictionary<K, V> AsConcurrentDictionary();

        /// <summary>
        /// Performs any pending maintenance operations needed by the cache. Exactly which activities are
        /// performed - if any - is implementation-dependent.
        /// </summary>
        void CleanUp();

        /// <summary>
        /// Returns access to inspect and perform low-level operations on this cache based on its runtime
        /// characteristics. These operations are optional and dependent on how the cache was constructed
        /// and what abilities the implementation exposes.
        /// </summary>
        IPolicy<K, V> Policy { get; }


    }
}
