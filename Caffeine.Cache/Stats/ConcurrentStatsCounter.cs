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
using System.Text;
using System.Threading;

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache.Stats
{
    /// <summary>
    /// A thread-safe <see cref="IStatsCounter"/> implementation for use by <see cref="ICache{K, V}"/> implementors.
    /// </summary>
    public sealed class ConcurrentStatsCounter : IStatsCounter
    {
        private long hitCount;
        private long missCount;
        private long loadSuccessCount;
        private long loadFailureCount;
        private long totalLoadTime;
        private long evictionCount;
        private long evictionWeight;

        public ConcurrentStatsCounter()
        {
            hitCount = 0;
            missCount = 0;
            loadSuccessCount = 0;
            loadFailureCount = 0;
            totalLoadTime = 0;
            evictionCount = 0;
            evictionWeight = 0;
        }

        public void RecordEviction(int weight)
        {
            Interlocked.Increment(ref evictionCount);
            Interlocked.Add(ref evictionWeight, weight);
        }

        public void RecordHits(int count)
        {
            Interlocked.Add(ref hitCount, count);
        }

        public void RecordLoadFailure(long loadTime)
        {
            Interlocked.Increment(ref loadFailureCount);
            Interlocked.Add(ref totalLoadTime, loadTime);
        }

        public void RecordLoadSuccess(long loadTime)
        {
            Interlocked.Increment(ref loadSuccessCount);
            Interlocked.Add(ref totalLoadTime, loadTime);
        }

        public void RecordMisses(int count)
        {
            Interlocked.Add(ref missCount, count);
        }

        public CacheStats Snapshot()
        {
            return new CacheStats((ulong)hitCount, (ulong)missCount, (ulong)loadSuccessCount, (ulong)loadFailureCount, (ulong)totalLoadTime, (ulong)evictionCount, (ulong)evictionWeight);
        }

        public void IncrementBy(IStatsCounter other)
        {
            CacheStats otherStats = other.Snapshot();
            Interlocked.Add(ref hitCount, (long)otherStats.HitCount);
            Interlocked.Add(ref missCount, (long)otherStats.MissCount);
            Interlocked.Add(ref loadSuccessCount, (long)otherStats.LoadSuccessCount);
            Interlocked.Add(ref loadFailureCount, (long)otherStats.LoadFailureCount);
            Interlocked.Add(ref totalLoadTime, (long)otherStats.TotalLoadTime);
            Interlocked.Add(ref evictionCount, (long)otherStats.EvictionCount);
            Interlocked.Add(ref evictionWeight, (long)otherStats.EvictionWeight);
        }

        public override string ToString()
        {
            return Snapshot().ToString();
        }
    }
}
