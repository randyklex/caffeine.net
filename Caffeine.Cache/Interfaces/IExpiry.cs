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
    /// <summary>
    /// Calculates when cache entries expire. A single expiration time is retained
    /// so that the lifetime of an entry may be extended or reduced by subsequent
    /// evaluations.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IExpiry<K, V>
    {
        /// <summary>
        /// specifies that the entry should be automatically removed from the cache
        /// once the duration has elapsed after the entry's creation. To indicate
        /// no expiration an entry may be given an excessively long period, such
        /// as long.MaxValue.
        /// 
        /// NOTE: The currentTime is supplied by the configured Ticker and by default
        /// does not relate to system or wall-clock time. When calculating the duration
        /// based on a time stamp, the current time should be obtained independently.
        /// </summary>
        /// <param name="key">The key represented by this entry.</param>
        /// <param name="value">The value represented by this entry.</param>
        /// <param name="currentTime">the current time in nanoseconds</param>
        /// <returns>the lenght of time before the entry expires, in nanoseconds.</returns>
        long ExpireAfterCreate(K key, V value, long currentTime);

        /// <summary>
        /// Specifies that the entry should be automatically removed from teh cache once
        /// the duration has elapsed after the replacement of its value. To indicate no
        /// expiration, an entry may be given an excessively long period, such as long.MaxValue.
        /// The currentDuration may be returned to not modify the expiration time.
        /// 
        /// NOTE: The currentTime is supplised by the configured Ticker and by default does
        /// not  relate to system or wall-clock time. When calculating the duration based
        /// on a time stamp, the current time should be obtained independently.
        /// </summary>
        /// <param name="key">The key represented by this entry.</param>
        /// <param name="value">The value represented by this entry</param>
        /// <param name="currentTime">the current time in nanoseconds</param>
        /// <param name="currentDuration">the current duration in nanoseconds</param>
        /// <returns>The lenght of time before the entry expires in nanoseconds</returns>
        long ExpireAfterUpdate(K key, V value, long currentTime, long currentDuration);

        /// <summary>
        /// Specifies that the entry should be automaitcally removed from teh cache once
        /// the duration has elapsed after its last read. To indicate no expiration an 
        /// entry may be given an excessively long period, such as long.MaxValue. The
        /// currentDuration may be returned to not modify the expiration time.
        /// 
        /// NOTE: The currenTime is supplised by the configured Ticker and by default
        /// does not relate to system or wall-clock time. When calculating the duration
        /// based on a time stamp, the current time should be obtained independently.
        /// </summary>
        /// <param name="key">The key represented by this entry.</param>
        /// <param name="value">The value represented by this entry.</param>
        /// <param name="currentTime">Current time in nanoseconds</param>
        /// <param name="currentDuration">the current duration in nanoseconds./param>
        /// <returns>length of time before the entry expires, in nanoseconds<</returns>
        long ExpireAfterRead(K key, V value, long currentTime, long currentDuration);
    }
}
