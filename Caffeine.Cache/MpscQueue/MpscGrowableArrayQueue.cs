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

namespace Caffeine.Cache.MpscQueue
{
    /// <summary>
    /// A multi-producer, single-consumer array queue which starts at <see cref="InitialCapacity"/> and grows to <see cref="MaxCapacity"/>
    /// in linked chunks of the initial size. The queue grows only when the current buffer is full and elements are not copied
    /// on resize, instead a link to the new buffer is stored in the old buffer for the consumer to follow.
    /// </summary>
    /// <typeparam name="E"></typeparam>
    internal sealed class MpscGrowableArrayQueue<E> : MpscChunkedArrayQueuecs<E> where E : class
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="initialCapacity">the queue initial capacity. If chunk size is fixed this will be the chunk size. Must be 2 or more.</param>
        /// <param name="maxCapacity">the maximum capacity will be rounded up to the closest power of 2 and will be the upper limit of 
        /// number of elements in this queue. Must be 4 or more and round up to a larger power of 2 than initial capacity.</param>
        public MpscGrowableArrayQueue(int initialCapacity, int maxCapacity)
            : base(initialCapacity, maxCapacity)
        { }

        protected override int GetNextBufferSize(E[] buffer)
        {
            long maxSize = maxQueueCapacity / 2;

            if (buffer.Length > maxSize)
                throw new InvalidOperationException();

            int newSize = 2 * (buffer.Length - 1);
            return newSize + 1;
        }

        protected override long GetCurrentBufferCapacity(long mask)
        {
            return (mask + 2 == maxQueueCapacity) ? maxQueueCapacity : mask;
        }
    }
}
