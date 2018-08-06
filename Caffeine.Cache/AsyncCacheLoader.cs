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
    /*
     * Computes or retrieves values asynchronously, based on a key, for use in populating
     * a AsyncLoadingCache.
     * 
     * Most implementations will only need to implement LoadAsync. Other methods may be 
     * overriden as desired.
     * 
     * Usage Example:
     * 
     * 
     */
    public abstract class AsyncCacheLoader<K, V>
    {
        /// <summary>
        /// Asynchronously computes or retrieves the value corresponding to Key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public abstract Task<V> LoadAsync(K key);

        /// <summary>
        /// Asynchronously computes or retrieves the values corresponding to <paramref name="keys"/>.
        /// This method is called by <see cref="AsyncCacheLoader{K, V}.GetAll()"/>.
        /// 
        /// If the returned map doesn't contain all requested <paramref name="keys"/> then the entries
        /// it does contain will be cached and GetAll will return the partial results. If the returned map
        /// contains extra keys not present in <paramref name="keys"/> then all returned entries will be
        /// cached, but only the entries for <paramref name="keys"/> will be returned from GetAll.
        /// 
        /// This method should be override when bulk retrieval is significantly more efficient than
        /// many individual lookups. Note that AsyncLoadingCache.GetAll will defer to individual calls to 
        /// AsyncLoadingCache.Get if this method is not overriden.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        // TODO: Original java passed in an Executor but with .NET ThreadPOols and TPL I don't think we have to do that..?? but figure it out..
        // TODO: This is a virtual function with a notimplemented exception.. why not just make it abstract and force everyone to impelemnt?
        public virtual Task<Dictionary<K, V>> LoadAllAsync(IEnumerable<K> keys)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously comptues or retrieves a replacement value corresponding to an already-cached
        /// <paramref name="key"/>. If the replacement value is not found then the mapping will be
        /// removed if <see langword="null"/> is computed. This method is called when an existing cache
        /// entry is refreshed by <see cref="Caffeine{K, V}.RefreshAfterWrite"/> or through a call to 
        /// LoadingCache.Refresh.
        /// 
        /// NOTE: All exceptions thrown by this method will be logged and then swallowed.
        /// </summary>
        /// <param name="key">The non-null key whose value should be loaded.</param>
        /// <param name="oldValue">the non-null old value corresponding to the key.</param>
        /// <returns></returns>
        // TODO: Original java passed in an Executor but with .NET ThreadPOols and TPL I don't think we have to do that..?? but figure it out..
        public virtual Task<V> ReloadAsync(K key, V oldValue)
        {
            return LoadAsync(key);
        }
    }
}
