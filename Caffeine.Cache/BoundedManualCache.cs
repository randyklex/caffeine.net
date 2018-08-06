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

using Caffeine.Cache.Factories;

namespace Caffeine.Cache
{
    public class BoundedManualCache<K, V> : ManualCache<BoundedLocalCache<K, V>, K, V>
    {
        private readonly bool isWeighted;
        private IPolicy<K, V> policy;

        public BoundedManualCache(Caffeine<K, V> builder)
            : this(builder, null)
        { }

        public BoundedManualCache(Caffeine<K, V> builder, CacheLoader<K, V> loader)
        {
            cache = CacheFactory<K, V>.Instance.NewBoundedLocalCache(builder, loader, false);
            isWeighted = builder.IsWeighted;
        }

        public BoundedLocalCache<K, V> Cache
        {
            get { return cache; }
        }

        public override IPolicy<K, V> Policy
        {
            get
            {
                if (policy == null)
                    policy = new BoundedPolicy<K, V>(cache, (item) => { return item; }, isWeighted);

                return policy;
            }
        }
    }
}
