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

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache.Stats
{
    internal sealed class GuardedStatsCounter : IStatsCounter
    {
        readonly IStatsCounter @delegate;

        public GuardedStatsCounter(IStatsCounter @delegate)
        {
            this.@delegate = @delegate;
        }

        public void RecordEviction(int weight)
        {
            try
            {
                @delegate.RecordEviction(weight);
            }
            catch (Exception e)
            { }
        }

        public void RecordHits(int count)
        {
            try
            {
                @delegate.RecordHits(count);
            }
            catch(Exception e)
            { }
        }

        public void RecordLoadFailure(long loadTime)
        {
            try
            {
                @delegate.RecordLoadFailure(loadTime);
            }
            catch (Exception e)
            { }
        }

        public void RecordLoadSuccess(long loadTime)
        {
            try
            {
                @delegate.RecordLoadSuccess(loadTime);
            }
            catch (Exception e)
            { }
        }

        public void RecordMisses(int count)
        {
            try
            {
                @delegate.RecordMisses(count);
            }
            catch (Exception e)
            { }
        }

        public CacheStats Snapshot()
        {
            
            try
            {
                return @delegate.Snapshot();
            }
            catch (Exception e)
            {
                return CacheStats.EmptyStats;
            }
        }

        public override string ToString()
        {
            return @delegate.ToString();
        }
    }
}
