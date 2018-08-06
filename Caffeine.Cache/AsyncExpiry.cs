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
using System.Threading.Tasks;

namespace Caffeine.Cache
{
    public class AsyncExpiry<K, V> : IExpiry<K, TaskCompletionSource<V>>
    {
        // TODO: what are the units here? Nanoseconds? Quote in original code, says this is "220 years".
        internal static long ASYNC_EXPIRY = (long.MaxValue >> 1) + (long.MaxValue >> 2);

        readonly IExpiry<K, V> @delegate;

        public AsyncExpiry(IExpiry<K, V> @delegate)
        {
            this.@delegate = @delegate ?? throw new ArgumentNullException("@delegate", "expiry cannot be NULL.");
        }

        public long ExpireAfterCreate(K key, TaskCompletionSource<V> value, long currentTime)
        {
            if (value.Task.IsCompletedSuccessfully)
            {
                long duration = @delegate.ExpireAfterCreate(key, value.Task.Result, currentTime);
                return Math.Min(duration, BoundedLocalCache<K, V>.MAXIMUM_EXPIRY);
            }

            return ASYNC_EXPIRY;
        }

        public long ExpireAfterUpdate(K key, TaskCompletionSource<V> value, long currentTime, long currentDuration)
        {
            if (value.Task.IsCompletedSuccessfully)
            {
                long duration = (currentDuration > BoundedLocalCache<K, V>.MAXIMUM_EXPIRY) ? @delegate.ExpireAfterCreate(key, value.Task.Result, currentTime) : @delegate.ExpireAfterUpdate(key, value.Task.Result, currentTime, currentDuration);
                return Math.Min(duration, BoundedLocalCache<K, V>.MAXIMUM_EXPIRY);
            }

            return ASYNC_EXPIRY;
        }

        public long ExpireAfterRead(K key, TaskCompletionSource<V> value, long currentTime, long currentDuration)
        {
            if (value.Task.IsCompletedSuccessfully)
            {
                long duration = @delegate.ExpireAfterRead(key, value.Task.Result, currentTime, currentDuration);
                return Math.Min(duration, BoundedLocalCache<K, V>.MAXIMUM_EXPIRY);
            }

            return ASYNC_EXPIRY;
        }
    }
}
