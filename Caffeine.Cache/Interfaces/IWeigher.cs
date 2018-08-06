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

using System.Threading.Tasks;

namespace Caffeine.Cache
{
    public interface IWeigher<K, V>
    {
        /// <summary>
        /// Returns the weight of a cache entry. There is no unit for entry weights; rather
        /// they are simply relative to each other.
        /// </summary>
        /// <param name="key">the key to weigh</param>
        /// <param name="value">the value to weight</param>
        /// <returns>the weight of the entry.</returns>
        int Weigh(K key, V value);

        /// <summary>
        /// Returns the weight of a cache entry. There is no unit for entry weights; rather
        /// they are simply relative to each other.
        /// </summary>
        /// <param name="key">the key to weigh</param>
        /// <param name="value">the value to weight</param>
        /// <returns>the weight of the entry.</returns>
        int WeighAsync(K key, TaskCompletionSource<V> value);
    }
}
