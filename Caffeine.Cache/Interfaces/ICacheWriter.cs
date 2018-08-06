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

namespace Caffeine.Cache.Interfaces
{
    public interface ICacheWriter<K, V>
    {
        /// <summary>
        /// Writes the value corresponding to the Key to the external resource. The cache will
        /// communicate a write when an entry in the cache is created or modified, except
        /// when that was due to a load or computation.
        /// </summary>
        /// <param name="key">The non-null key whose value should be written.</param>
        /// <param name="value">The value associated with the Key that should be written.</param>
        void Write(K key, V value);

        /// <summary>
        /// Deletes the value corresponding to the Key from the external resource. The cache
        /// will communicate a delete when the entry is explicitly removed or evicted.
        /// </summary>
        /// <param name="key">The non-null Key whose value was removed.</param>
        /// <param name="value">The value associated with the Key or NULL if collected.</param>
        /// <param name="cause">The reason for which the entry was removed.</param>
        void Delete(K key, V value, RemovalCause cause);
    }
}
