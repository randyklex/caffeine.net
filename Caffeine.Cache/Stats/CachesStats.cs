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
using System.Diagnostics.Contracts;
using System.Text;

namespace Caffeine.Cache.Stats
{
    public sealed class CacheStats
    {
        private readonly ulong hitCount;
        private readonly ulong missCount;
        private readonly ulong loadSuccessCount;
        private readonly ulong loadFailureCount;
        private readonly ulong totalLoadTime;
        private readonly ulong evictionCount;
        private readonly ulong evictionWeight;

        public static readonly CacheStats EmptyStats = new CacheStats(0, 0, 0, 0, 0, 0, 0);

        public CacheStats(ulong hitCount, ulong missCount, ulong loadSuccessCount, ulong loadFailureCount, ulong totalLoadTime, ulong evictionCount, ulong evictionWeight)
        {

            this.hitCount = hitCount;
            this.missCount = missCount;
            this.loadSuccessCount = loadSuccessCount;
            this.loadFailureCount = loadFailureCount;
            this.totalLoadTime = totalLoadTime;
            this.evictionCount = evictionCount;
            this.evictionWeight = evictionWeight;
        }

        /// <summary>
        /// Returns the number of times <see cref="ICache{K, V}"/> lookup methods have returned either a cached
        /// or uncached value. This is defined as hitCount + missCount.
        /// </summary>
        public ulong RequestCount
        {
            get { return hitCount + missCount; }
        }

        public ulong HitCount
        {
            get { return hitCount; }
        }

        public double HitRate
        {
            get
            {
                ulong request = RequestCount;
                return (request == 0) ? 1.0d : (double)HitCount / request;
            }
        }

        public ulong MissCount
        {
            get { return missCount; }
        }

        /// <summary>
        /// Returns the total number of times that <see cref="ICache{K, V}"/> lookup methods attempted
        /// to load new values. This includes both successful load operations, as well as those that
        /// threw exceptions. This is defined as <see cref="LoadSuccessCount"/> + <see cref="LoadFailureCount"/>
        /// </summary>
        public ulong LoadCount
        {
            get { return loadSuccessCount + loadFailureCount; }
        }

        /// <summary>
        /// Returns the number of times <see cref="ICache{K, V}"/> lookup methods have successfully loaded
        /// a new value. This is always incremented in conjunction with <see cref="MissCount"/>, though
        /// <see cref="MissCount"/> is also incremented when an exception is encountered during cache loading.
        /// Multiple concurrent misses for the same key will result in a single load operation.
        /// </summary>
        public ulong LoadSuccessCount
        {
            get { return loadSuccessCount; }
        }
        
        /// <summary>
        /// Returns the number of times <see cref="ICache{K, V}"/> lookup methods failued to load a new value,
        /// either because no value was found or an exception was thrown while loading. This is always 
        /// incremented in conjunction with <see cref="MissCount"/>, though <see cref="MissCount"/> is also
        /// incremented when cache loading completes successfully (see <see cref="LoadSuccessCount"/>. 
        /// Multiple concurrent misses for the same key will result in a single load operation.
        /// </summary>
        public ulong LoadFailureCount
        {
            get { return loadFailureCount; }
        }

        public double LoadFailureRate
        {
            get
            {
                ulong totalLoadCount = loadSuccessCount + loadFailureCount;
                return (totalLoadCount == 0) ? 0.0d : (double)loadFailureCount / totalLoadCount;
            }
        }

        /// <summary>
        /// Returns the total number of nanoseconds the cache has spent loading new values. This can be
        /// used to calcualte the miss penalty. This value is increased every time <see cref="LoadSuccessCount"/>
        /// or <see cref="LoadFailureCount"/> is incremented.
        /// </summary>
        public ulong TotalLoadTime
        {
            get { return totalLoadTime; }
        }

        /// <summary>
        /// Returns the average time spent loading new values. This is defined as <see cref="TotalLoadTime"/> / (<see cref="LoadSuccessCount"/> + <see cref="LoadFailureCount"/>).
        /// </summary>
        public double AverageLoadPenalty
        {
            get
            {
                ulong totalLoadCount = loadSuccessCount + loadFailureCount;
                return (totalLoadCount == 0) ? 0.0d : (double)totalLoadTime / totalLoadCount;
            }
        }

        /// <summary>
        /// Returns the number of times an entry has been evicted. This count does not include manual invalidations.
        /// </summary>
        public ulong EvictionCount
        {
            get { return evictionCount; }
        }

        /// <summary>
        /// Returns the sum of weights of evicted entries. This total does not include manual invalidations.
        /// </summary>
        public ulong EvictionWeight
        {
            get { return evictionWeight; }
        }

        /// <summary>
        /// Subetracts a <see cref="CacheStats"/> from another <see cref="CacheStats"/>
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <returns></returns>
        public static CacheStats operator -(CacheStats c1, CacheStats c2)
        {
            return new CacheStats(
                Math.Max(0L, c1.hitCount - c2.HitCount),
                Math.Max(0L, c1.missCount - c2.MissCount),
                Math.Max(0L, c1.loadSuccessCount - c2.loadSuccessCount),
                Math.Max(0L, c1.loadFailureCount - c2.loadFailureCount),
                Math.Max(0L, c1.totalLoadTime - c2.totalLoadTime),
                Math.Max(0L, c1.evictionCount - c2.evictionCount),
                Math.Max(0L, c1.evictionWeight - c2.evictionWeight));
        }

        public static CacheStats operator +(CacheStats c1, CacheStats c2)
        {
            return new CacheStats(
                c1.hitCount + c2.hitCount,
                c1.missCount + c2.missCount,
                c1.loadSuccessCount + c2.loadSuccessCount,
                c1.loadFailureCount + c2.loadFailureCount,
                c1.totalLoadTime + c2.totalLoadTime,
                c1.evictionCount + c2.evictionCount,
                c1.evictionWeight + c2.evictionWeight);
        }

        public override string ToString()
        {
            return string.Format("{0} + {{ hitcount = {1}, missCount = {2}, loadSuccessCount = {3}, loadFailureCount = {4}, totalLoadTime = {5}, evictionCount = {6}, evictionWeight = {7} }}", this.GetType().Name, hitCount, missCount, loadSuccessCount, loadFailureCount, totalLoadTime, evictionCount, evictionWeight);
        }
    }
}
