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

namespace Caffeine.Cache
{
    /// <summary>
    /// A cache that provides the following features.
    /// <list type="bullet">
    /// <item><description>StrongKeys</description></item>
    /// <item><description>StrongValues</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class BoundedLocalCacheStrongKeyStrongValue<K, V> : BoundedLocalCache<K, V>
    {
        public BoundedLocalCacheStrongKeyStrongValue(Caffeine<K, V> builder, CacheLoader<K, V> loader, bool isAsync)
            : base(builder, loader, isAsync)
        { }
    }
}
