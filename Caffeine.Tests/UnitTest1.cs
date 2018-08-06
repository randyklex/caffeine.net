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
using System.Threading;

using Xunit;
using Xunit.Abstractions;

using Caffeine.Cache;


namespace Caffeine.Tests
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper output;

        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CacheItem()
        {
            ICache<string, string> cache = Caffeine<string, string>.Builder()
                .SpecifyMaximumSize(100)
                .RecordStats()
                //.ExpireAfterWrite(new TimeSpan(0, 1, 0))
                //.RefreshAfterWrite(new TimeSpan(0, 1, 0))
                .Build();

            cache.Add("test", "hello world");

            //Thread.Sleep(5000);
            string rval = cache.TryGetValue("test");

            output.WriteLine(cache.Stats.ToString());

            Assert.Equal("hello world", rval);
        }

        [Fact]
        public void CacheAndRemoveItem()
        {
            ICache<string, string> cache = Caffeine<string, string>.Builder()
                .SpecifyMaximumSize(100)
                .RecordStats()
                //.ExpireAfterWrite(new TimeSpan(0, 1, 0))
                //.RefreshAfterWrite(new TimeSpan(0, 1, 0))
                .Build();

            cache.Add("test", "hello world");

            cache.Invalidate("test");
            
            string rval = cache.TryGetValue("test");

            Assert.True(string.IsNullOrWhiteSpace(rval));
        }

        [Fact]
        public void CacheMultipleItems()
        {
            ICache<string, string> cache = Caffeine<string, string>.Builder()
                .SpecifyMaximumSize(2)
                .RecordStats()
                //.ExpireAfterWrite(new TimeSpan(0, 1, 0))
                //.RefreshAfterWrite(new TimeSpan(0, 1, 0))
                .Build();

            cache.Add("test", "hello world");
            cache.Add("new", "secondItem");

            string rval = cache.TryGetValue("new");

            Assert.True(!string.IsNullOrWhiteSpace(rval));
        }
    }
}
