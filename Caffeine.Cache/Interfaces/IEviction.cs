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
    /// <summary>
    /// Low level operations for a cache with a size-based eviction policy.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IEviction<K, V>
    {
        /// <summary>
        /// Whether the cache is bounded by a maximum size or maximum weight.
        /// </summary>
        bool IsWeighted { get; }

        /// <summary>
        /// The weight of the entry.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        int? WeightOf(K key);

        /// <summary>
        /// The approximate accumulated weight of entries in this cache.
        /// </summary>
        ulong? WeightedSize { get; }

        /// <summary>
        /// Maximum total weighted or unweighted size of this cache, depending
        /// on how the cache was constructed. This value can be best understood
        /// by inspecting IsWeighted.
        /// </summary>
        ulong Maximum { get; set; }

        /// <summary>
        /// An unmodifiable snapshot view of the cache with ordered traversal. The
        /// order of iteration is from the entries least likely to be retained (coldest)
        /// to the entries most likely to be retained (hottest). This order is determined
        /// by the eviction policy's best guess at the time of creating this snapshot view.
        /// 
        /// Beware that obtaining the mappings is NOT a constant-time operation. Because of the
        /// asynchronous nature of the page replacement policy, determining the retention
        /// ordering requires a traversal of the entries.
        /// </summary>
        /// <param name="limit">The maximum size of the returned dictionary. Use int.MaxValue to disregard the limit.</param>
        /// <returns></returns>
        SortedDictionary<K, V> Coldest(uint limit);

        /// <summary>
        /// Returns an unmodifiable snapshot view of the cache with ordered traversal.
        /// The order of iteration is from the entries most likely to be retained (hottest) to
        /// the entries least likely to be retained (coldest). This order is determined by
        /// the eviction policy's best guess at the time of creating this snapshot view.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        SortedDictionary<K, V> Hottest(uint limit);
    }
}
