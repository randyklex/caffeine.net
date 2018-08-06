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

using System.Collections.Specialized;

namespace Caffeine.Cache
{
    public interface IVariableExpiration<K, V>
    {
        /// <summary>
        /// Returns the duration until the entry should be automatically removed. The expiration
        /// policy determines when the entry's duration is reset.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        long? GetExpiresAfter(K key);

        /// <summary>
        /// Specifies that the entry should be automatically removed from the cache once the duration has
        /// elapsed. The expiration policy determines when the entry's age is reset.
        /// </summary>
        /// <param name="key">Key for the entry being set</param>
        /// <param name="duration">The length of time from now when the entry should be automatically removed (in nanoseconds).</param>
        void SetExpiresAfter(K key, ulong duration);

        /// <summary>
        /// Associates the <paramref name="value"/> with the <paramref name="key"/> in this cache if the 
        /// specified key is not already associated with a value. This method differs from Map.PutIfAbsent by
        /// substituting the configured <see cref="IExpiry{K, V}"/> with the specified write duration, 
        /// has no effect on the duration if the entry was present, and returns the success rather than a value.
        /// </summary>
        /// <param name="key">The key with which the specified value is to be associated.</param>
        /// <param name="value">Value to be associated with the specified key.</param>
        /// <param name="duration">The length of time from now when the entry should be automatically removed.</param>
        /// <returns>true if this cache did not already contain the specified entry.</returns>
        bool TryAdd(K key, V value, long duration);

        /// <summary>
        /// Associates the <paramref name="value"/> with the <paramref name="key"/> in this
        /// cache. If the cache previously contained a value associated with the <paramref name="key"/>,
        /// the old value is replaced by the new <paramref name="value"/>. This method differs from <see cref="ICache{K, V}.Add(K, V)"/>
        /// by substituting the configured <see cref="IExpiry{K, V}"/> with the specified <paramref name="duration"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="duration"></param>
        void Put(K key, V value, long duration);

        /// <summary>
        /// Returns an unmodifiable snapshot <see cref="IOrderedDictionary"/> view of the cache with ordered
        /// traversal. The order of iteration is from the entries most likely to expire (oldest)
        /// to the entries least likely to expire (youngest). This order is determined by the expiration
        /// policy's best guess at the time of creating this snapshot view.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        IOrderedDictionary Oldest(uint limit);

        /// <summary>
        /// Returns a snapshot <see cref="IOrderedDictionary"/> view of the cache with ordered
        /// traversal. The order of iteration is from the entries least likely to expire (youngest)
        /// to the entries most likely to expire (oldest). This order is determined by the
        /// expiration policy's best guess at the time of creating this snapshot view.
        /// </summary>
        /// <param name="limit">the maximum size of the returned map (use <see cref="System.UInt32.MaxValue"/> to disregard the limit)</param>
        /// <returns>A snapshot view of the cache from youngest entry to the oldest.</returns>
        IOrderedDictionary Youngest(uint limit);
    }
}
