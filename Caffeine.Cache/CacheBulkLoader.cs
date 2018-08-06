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
    public abstract class CacheBulkLoader<K, V> : CacheLoader<K, V>
    {
        public override bool HasBulkLoader { get { return true; } }

        /// <summary>
        /// Computes or retrieves the values correspondong to <paramref name="keys"/>. This method is called by <see cref="LoadingCache{C, K, V}.GetAll(IEnumerable{K})"/>
        /// <para>
        /// If the returned dictionary doesn't contain all requested <paramref name="keys"/> then the entries
        /// it does contain will be cached and <see cref="LoadingCache{C, K, V}.GetAll(IEnumerable{K})"/> will return
        /// the partial results. If the returned dictionary contains extra keys not present in <paramref name="keys"/> then
        /// all returned entries will be cached, but only entries for <paramref name="keys"/> will be returned from <see cref="LoadingCache{C, K, V}.GetAll(IEnumerable{K})"/>
        /// </para>
        /// <para>
        /// This method should be overridden when bulk retrieval is significanly more efficient than many individual lookups. Note that <see cref="LoadingCache{C, K, V}.GetAll(IEnumerable{K})"/>
        /// will defer to inidividual calls to <see cref="LoadingCache{C, K, V}.GetOrAdd(K)"/> if this method is not overridden.
        /// </para>
        /// <para>
        /// WARNING: Loading must not attempt to update any mappings of this cache directly.
        /// </para>
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public virtual Dictionary<K, V> LoadAll(IEnumerable<K> keys)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously computes or retrieves the values corresponding to <paramref name="keys"/>. This 
        /// method is called by <see cref="LocalAsyncLoadingCache{C, K, V}.GetAllAsync(IEnumerable{K})"/>.
        /// 
        /// <para>
        /// If the returned dictionary doesn't contain all requested <paramref name="keys"/> then the entries
        /// it does contain will be cached and <see cref="LocalAsyncLoadingCache{C, K, V}.GetAllAsync(IEnumerable{K})"/> will
        /// return the partial results. If the returned dictionary contains extra keys not present in <paramref name="keys"/>
        /// then all returned entries will be cached, but only the entries for <paramref name="keys"/> will be returned
        /// from <see cref="LocalAsyncLoadingCache{C, K, V}.GetAllAsync(IEnumerable{K})"/>.
        /// </para>
        /// 
        /// <para>
        /// This method should be overridden when bulk retrievalis significanly more efficient than many individual
        /// lookups. Note that <see cref="LocalAsyncLoadingCache{C, K, V}.GetAllAsync(IEnumerable{K})"/> will defer
        /// to individual calls to <see cref="LocalAsyncLoadingCache{C, K, V}.GetAsync(K)"/> if this method is not
        /// overridden.
        /// </para>
        /// </summary>
        /// <param name="keys">The unique, non-null keys whose values should be loaded.</param>
        /// <returns></returns>
        public override Task<Dictionary<K, V>> LoadAllAsync(IEnumerable<K> keys)
        {
            if (keys == null)
                throw new ArgumentNullException("keys", "keys cannot be null.");

            return new Task<Dictionary<K, V>>(() =>
            {
                return LoadAll(keys);
            });
        }
    }
}
