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

namespace Caffeine.Cache
{
    public interface IPolicy<K, V>
    {
        /// <summary>
        /// Returns whether the cache statistics are being accumulated.
        /// </summary>
        bool IsRecordingStats { get; }

        /// <summary>
        /// Returns access to perform operations based on the maximum size or maximum
        /// weight eviction policy. If the cache was not constructed with a size-based
        /// bound or the implementation does not support these operations, null is returned.
        /// </summary>
        IEviction<K, V> Eviction { get; }

        /// <summary>
        /// Returns access to perform operations based on the time-to-idle expiration
        /// policy. This policy determines that an entry should be automatically removed from
        /// the cache once a fixed duration has elapsed after the entry's creation, the
        /// most recent replacement of its value, or its last access. Access time is reset by
        /// all cache read and write operations.
        /// 
        /// If the cache was not constructed with access-based expiration or the implementation
        /// does not support these operations, a NULL is returned.
        /// </summary>
        IExpiration<K, V> ExpireAfterAccess { get;  }

        IExpiration<K, V> ExpireAfterWrite { get; }

        IExpiration<K, V> RefreshAfterWrite { get; }

        IVariableExpiration<K, V> ExpireVariably { get; }
    }
}
