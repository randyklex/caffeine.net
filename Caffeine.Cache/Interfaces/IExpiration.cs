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
using System.Collections.Generic;

namespace Caffeine.Cache
{
    public interface IExpiration<K, V>
    {
        /// <summary>
        /// Returns the age of the entry based on the expiration policy. The entry's age is the cache's
        /// estimate of the amount of time since the entry's expiration was last reset.
        /// 
        /// An expiration policy uses the age to determine if an entry is fresh or stale by comparing
        /// it to the freshness lifetime. This is calculated as <code>fresh = freshnessLifetime > age;</code>
        /// where <code>freshnessLifetime = expires - currentTime;</code>
        /// </summary>
        /// <param name="key">key for the entry being queried</param>
        /// <returns>the age if the entry is present in the cache</returns>
        long? AgeOf(K key);

        /// <summary>
        /// Returns the fixed duration used to determine if an entry should be automatically removed due
        /// to elapsing this time bound. An entry is considered fresh if its age is less than this duration,
        /// and stale otherwise. The expiration policy determines when the entry's age is reset.
        /// 
        /// value is always expressed in nanoseconds.
        /// </summary>
        long ExpiresAfter { get; set;  }

        /// <summary>
        /// Returns a snapshot <see cref="IOrderedDictionary"/> view of the cache with ordered
        /// traversal. The order of iteration is from the entries most likely to expire (oldest)
        /// to the entries least likely to expire (youngest). This order is determined by the expiration
        /// policy's best guess at the time of creating this snapshot view.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        IOrderedDictionary Oldest(uint limit);

        /// <summary>
        /// Returns a snapshot <see cref="Dictionary{TKey, TValue}"/> view of the cache with ordered
        /// traversal. The order of iteration is from the entries least likely to expire (youngest)
        /// to the entries most likely to expire (oldest). This order is determined by the
        /// expiration policy's best guess at the time of creating this snapshot view.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        IOrderedDictionary Youngest(uint limit);
    }
}
