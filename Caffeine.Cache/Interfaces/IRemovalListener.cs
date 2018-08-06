﻿/*
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
    /// <summary>
    /// An object that can receive a notification when an entry is removed
    /// from a cache. The removal resulting in notification could have occurred
    /// to an entry being manually removed or replaced, or due to an eviction
    /// resulting from timed expiration, excueeding a maximum size or
    /// garbage collection.
    /// 
    /// An instance may be called concurrently by multiple threads to process 
    /// different entries. Implementations of this interface should avoid 
    /// performing blocking calls or synchronizing on shared resources.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IRemovalListener<K, V>
    {
        void OnRemoval(K key, V value, RemovalCause cause);

        void OnRemovalAsync(K key, Task<V> computableValue, RemovalCause cause);
    }
}
