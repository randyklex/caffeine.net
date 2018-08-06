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

using System.Threading.Tasks;

namespace Caffeine.Cache
{
    public class UnboundedAsyncLoadingCache<K, V> : LocalAsyncLoadingCache<UnboundedLocalCache<K, Task<V>>, K, V>
    {
        private IPolicy<K, V> policy;

        public UnboundedAsyncLoadingCache(Caffeine<K, Task<V>> builder, AsyncCacheLoader<K, V> loader)
            : base(new UnboundedLocalCache<K, Task<V>>(builder, true), loader)
        { }

        protected override IPolicy<K, V> Policy
        {
            get
            {
                if (policy == null)
                    policy = new UnboundedPolicy<K, V>(cache.IsRecordingStats);

                return policy;
            }
        }
    }
}
