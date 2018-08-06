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

using Caffeine.Cache.Stats;

namespace Caffeine.Cache.Interfaces
{
    public interface IStatsCounter
    {
        /// <summary>
        /// Records cache hits. This should be called when a cache request returns a cached value.
        /// </summary>
        /// <param name="count">The number of hits to record.</param>
        void RecordHits(int count);

        /// <summary>
        /// Records cache misses. This should be called when a cache request returns a value that
        /// was not found in the cache. This method should be called by the loading thread, as
        /// well as by threads blocking on the load. Multiple concurrent calls to <see cref="Cache"/>
        /// lookup methods with the same key on an absent value should result in a single call to either
        /// <see cref="RecordLoadSuccess"/> or <see cref="RecordLoadFailure"/> and multiple calls to this
        /// method, despite all being served by the results of a single load operation.
        /// </summary>
        /// <param name="count"></param>
        void RecordMisses(int count);

        /// <summary>
        /// Records the successful load of a new entry. This should be called when a cache request
        /// causes an entry to be loaded, but either no value is found or an exception is thrown while
        /// loading the entry. In contrast to <see cref="RecordMisses(int)"/>, this method should only 
        /// be called by the loading thread.
        /// </summary>
        /// <param name="loadTime"></param>
        void RecordLoadSuccess(long loadTime);

        /// <summary>
        /// Records the failed load of a new entry. This should be called when a cache request causes an entry
        /// to be loaded, but iether no value is found or an exception is thrown while loading the entry.
        /// In contrast to <see cref="RecordMisses(int)"/>, this method should only be called by the loading
        /// thread.
        /// </summary>
        /// <param name="loadTime">The number of nanoseconds the cache spent computing or retrieving the new
        /// value prior to discovering the value doesn't exist or an exception being thrown.</param>
        void RecordLoadFailure(long loadTime);

        /// <summary>
        /// Records the eviction of an entry from the cache. This should only be called when an entry
        /// is evicted due to the cache's eviction strategy, and not as a result of manual invalidations.
        /// </summary>
        /// <param name="weight">The weight of the evicted entry.</param>
        void RecordEviction(int weight);

        /// <summary>
        /// Returns a snapshot of this counter's values. Note taht this may be an inconsistent view,
        /// as it may be interleaved with update operations.
        /// </summary>
        /// <returns>A snapshot of this counter's values.</returns>
        CacheStats Snapshot();
    }
}
