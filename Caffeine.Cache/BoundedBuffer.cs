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
    /// A striped, non-blocking bounded buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class BoundedBuffer<T> : StripedBuffer<T> where T : class
    {
        /*
         * A circular ring buffer stores the elements being transferred by the producers
         * to the consumer. The monotnically increasing count of reads and writes allow
         * indexing sequentially to the next element location based upon a power-of-otw sizing.
         * 
         * The producers race to read the counts, check if there is available capacity, and
         * if so then try once to CAS to the next write count. If the increment is successful
         * then the producer lazily publishes the element. The producer does not retry or block
         * when unsuccessful due to a failed CAS or the buffer being full.
         * 
         * The consumer reads the counts and takes the available elements. The clearing of the 
         * elements and the next read count are lazily set.
         * 
         * This implementation is striped to further increase concurrency by rehashing
         * and dynamically adding new buffers when contention is detected, up to an internal
         * maximum. When rehashing in order to discover an available buffer, the producer
         * may retry adding its element to determine whether it found a satisfactory buffer
         * or if resizing is necessary.
         * 
         */

        public BoundedBuffer()
            : base()
        { }

        protected override Buffer<T> Create(T e)
        {
            return new RingBuffer<T>(e);
        }
    }
}
