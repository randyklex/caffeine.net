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
    public abstract class CacheLoader<K, V> : AsyncCacheLoader<K, V>
    {
        public virtual bool HasBulkLoader { get { return false; } }

        /// <summary>
        /// Computes or retrieves the value correspondong to the <paramref name="key"/>.
        /// 
        /// <para>
        /// WARNING: Loading must not attempt to update any mappings on this cache directly.
        /// </para>
        /// </summary>
        /// <param name="key">The non-nukll key whose value should be loaded.</param>
        /// <returns>The value associated with <paramref name="key"/> or <see langword="null"/> if not found</returns>
        public abstract V Load(K key);

        public override Task<V> LoadAsync(K key)
        {
            if (EqualityComparer<K>.Default.Equals(key, default(K)))
                throw new ArgumentNullException("key", "key cannot be null.");

            return new Task<V>(() =>
            {
                return Load(key);
            });
        }

        public virtual V Reload(K key, V oldValue)
        {
            return Load(key);
        }

        public override Task<V> ReloadAsync(K key, V oldValue)
        {
            if (EqualityComparer<K>.Default.Equals(key, default(K)))
                throw new ArgumentNullException("key", "key cannot be null.");

            return new Task<V>(() =>
            {
                return Reload(key, oldValue);
            });
        }
    }
}
