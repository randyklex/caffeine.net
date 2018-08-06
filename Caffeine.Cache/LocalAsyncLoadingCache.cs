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

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache
{
    /// <summary>
    /// This class provides a skeletal implementation of the <see cref="IAsyncLoadingCache{K, V}"/> interface
    /// to minimize the effort required to implement a <see cref="LocalCache{K, V}"/>
    /// </summary>
    /// <typeparam name="C"></typeparam>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public abstract class LocalAsyncLoadingCache<C, K, V> : IAsyncLoadingCache<K, V> where C : LocalCache<K, Task<V>>
    {
        protected C cache;
        bool canBulkLoad;
        AsyncCacheLoader<K, V> mLoader;

        internal LocalAsyncLoadingCache(C cache, AsyncCacheLoader<K, V> loader)
        {
            this.mLoader = loader;
            //this.canBulkLoad = canBulkLoad(loader);
            this.cache = cache;
        }

        protected abstract IPolicy<K, V> Policy { get; }

        #region IAsyncLoadingCache<K,V>

        public Task<Dictionary<K, V>> GetAllAsync(IEnumerable<K> keys)
        {
            throw new NotImplementedException();
        }

        public Task<V> GetAsync(K key)
        {
            throw new NotImplementedException();
        }

        public Task<V> GetAsync(K key, Func<K, V> mappingFunction)
        {
            throw new NotImplementedException();
        }

        public Task<V> GetAsync(K key, Func<K, Task<V>> mappingFunction)
        {
            throw new NotImplementedException();
        }

        public Task<V> GetIfPresentAync(K key)
        {
            throw new NotImplementedException();
        }

        public Task PutAsync(K key, Task<V> valueTask)
        {
            throw new NotImplementedException();
        }

        public ILoadingCache<K, V> Synchronous()
        {
            throw new NotImplementedException();
        }

        #endregion // IAsyncLoadingCache<K,V>
    }
}
