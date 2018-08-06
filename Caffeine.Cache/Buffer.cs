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

namespace Caffeine.Cache
{
    public enum OfferStatusCodes
    {
        FAILED = -1,
        SUCCESS = 0,
        FULL = 1
    }

    /// <summary>
    /// A multiple-producer / single-consumer buffer that rejects new elements if it is full or
    /// fails spuriously due to contention. Unlike a queue and stack, a buffer does not
    /// guarantee an ordering of elements in either FIFO or LIFO order.
    /// 
    /// Beware that it is the responsibility of the caller to ensure that a consumer has exclusive
    /// read access to the buffer. This implementation does not include fail-fast behavior to
    /// guard against incorrect consumer usage.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Buffer<T>
    {
        protected Buffer()
        { }

        /// <summary>
        /// Inserts the specified element into this buffer if it is possible to do so
        /// immediately without violating capacity restrictions. The addition is allowed
        /// to fail spuriously if multiple threads insert concurrently.
        /// </summary>
        /// <param name="element">element to add</param>
        /// <returns>1 if the buffer is full, -1 if failed, or 0 if added.</returns>
        public abstract OfferStatusCodes Offer(T element);

        /// <summary>
        /// Drains the buffer, sending each element to the consumer for processing.
        /// The caller must ensure that a consumer has exclusive read access to
        /// the buffer.
        /// </summary>
        /// <param name="consumer">The action to perform on each element.</param>
        public abstract void DrainTo(Action<T> consumer);

        /// <summary>
        /// Returns thenumber of elements that have ben read from the buffer.
        /// </summary>
        /// <returns>The number of elements read from this buffer.</returns>
        // TODO: Note the diff between Java Caffeine in that we use unsigned here.
        public abstract uint Reads();

        /// <summary>
        /// Returns the number of elements that have been written to the buffer.
        /// </summary>
        /// <returns>the number of elements written to this buffer.</returns>
        // TODO: Note the diff between Java Caffeine in that we use unsigned here.
        public abstract uint Writes();

        /// <summary>
        /// Returns the number of elemnets residing in the buffer.
        /// </summary>
        /// <returns>the number of elements in this buffer.</returns>
        // TODO: the original property was called Size. Renamed to be more consistent with .NET nomenclature.
        public virtual uint Count()
        {
            return Writes() - Reads();
        }
    }
}
