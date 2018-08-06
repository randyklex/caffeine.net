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
using System.Threading;

namespace Caffeine.Cache.MpscQueue
{
    internal abstract class BaseMpscLinkedArrayQueue<T> : BaseMpscLinkedArrayQueueColdProducerFields<T> where T : class
    {
        private readonly static T JUMP = default(T);
        private readonly int REF_ELEMENT_SHIFT = IntPtr.Size == 4 ? 2 : 3; 

        public BaseMpscLinkedArrayQueue(int initialCapacity)
        {
            if (initialCapacity < 2)
                // TODO: conversion from JAVA here to a standard .NET exception from IllegalArgumentException
                throw new ArgumentOutOfRangeException("initial capacity must be 2 or more.");

            int p2capacity = Utility.CeilingNextPowerOfTwo(initialCapacity);

            // leave the lower bit of mask clear
            long mask = (p2capacity - 1L) << 1;

            // TODO: he called a method "allocate" which created an array of object[], and then casts to the desired type. maybe that's a JAVA quirk?
            // I think this allocate casted back to type T was so that he could store in an object array a pointer to the next buffer array..
            // +1 because we need extra element to point at next array
            T[] buffer = new T[p2capacity + 1];

            producerBuffer = buffer;
            producerMask = mask;
            consumerBuffer = buffer;
            consumerMask = mask;

            producerLimit = mask;
        }

        public override string ToString()
        {
            return GetType().Name + "@" + GetHashCode().ToString("x");
        }

        public bool Offer(T item)
        {
            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new NullReferenceException("item cannot be null.");

            long mask;
            T[] buffer;
            long pIndex;

            while (true)
            {
                long producerLimit = base.producerLimit;
                pIndex = base.producerIndex;

                // lower bit is indicative of resize, if we see it we spin until it's cleared
                if ((pIndex & 1) == 1)
                    continue;

                // mask/buffer may get changed by resizing -> only use for array access after successful CAS.
                mask = this.producerMask;
                buffer = this.producerBuffer;

                // assumption behind this optimization is that queue is almost always empty or near empty.
                if (producerLimit <= pIndex)
                {
                    int result = OfferSlowPath(mask, pIndex, producerLimit);
                    switch(result)
                    {
                        case 0:
                            break;
                        case 1:
                            continue;
                        case 2:
                            return false;
                        case 3:
                            Resize(mask, buffer, pIndex, item);
                            return true;
                    }
                }

                if (CasProducerIndex(pIndex, pIndex + 2))
                    break;
            }

            // index visible before element, consistent with consumer expectation.
            long offSet = ModifiedCalcElementOffset(pIndex, mask);
            buffer[offSet] = item;
            return true;
        }

        private bool CasProducerIndex(long expect, long newValue)
        {
            return Interlocked.CompareExchange(ref base.producerIndex, newValue, expect) == expect;
        }

        private long ModifiedCalcElementOffset(long index, long mask)
        {
            return (index & mask) << (REF_ELEMENT_SHIFT - 1);
        }

        /// <summary>
        /// We do not inline resize into this method because we do not resize on fill.
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="pIndex"></param>
        /// <param name="producerLimit"></param>
        /// <returns></returns>
        private int OfferSlowPath(long mask, long pIndex, long producerLimit)
        {
            int result;
            long cIndex = base.consumerIndex;
            long bufferCapacity = GetCurrentBufferCapacity(mask);

            result = 0; // 0 - goto pIndex CAS.

            if (cIndex + bufferCapacity > pIndex)
            {
                if (!CasProducerLimit(producerLimit, cIndex + bufferCapacity))
                    result = 1; // retry from top.
            }
            // full and cannot grow
            else if (AvailableInQueue(pIndex, cIndex) <= 0)
            {
                result = 2; // return false;
            }
            // grab index for resize -> set lower bit
            else if (CasProducerIndex(pIndex, pIndex + 1))
            {
                result = 3; // resize
            }
            else
                result = 1; // failed resize attempt, retry from top.

            return result;
        }

        /// <summary>
        /// Available elements in queue * 2.
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="cIndex"></param>
        /// <returns></returns>
        protected abstract long AvailableInQueue(long pIndex, long cIndex);

        /// <summary>
        /// This impelmentation is correct for single consumer thread use only.
        /// </summary>
        /// <returns></returns>
        public T Poll()
        {
            T[] buffer = consumerBuffer;
            long index = consumerIndex;
            long mask = consumerMask;

            long offSet = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offSet];
            if (EqualityComparer<T>.Default.Equals(item, default(T)))
            {
                if (index != base.producerIndex)
                {
                    do
                    {
                        item = buffer[offSet];

                    } while (EqualityComparer<T>.Default.Equals(item, default(T)));
                }
                else
                {
                    return default(T);
                }
            }

            if (EqualityComparer<T>.Default.Equals(item, JUMP))
            {
                T[] nextBuffer = GetNextBuffer(buffer, mask);
                return NewBufferPoll(nextBuffer, index);
            }

            buffer[offSet] = default(T);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);

            return item;
        }

        /// <summary>
        /// This impelmentation is correct for single consumer thread use only.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            T[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset];
            if (EqualityComparer<T>.Default.Equals(item, default(T)) && index != base.producerIndex)
            {
                while (EqualityComparer<T>.Default.Equals((item = buffer[offset]), default(T)))
                {
                    ;
                }
            }

            if (EqualityComparer<T>.Default.Equals(item, JUMP))
                return NewBufferPeek(GetNextBuffer(buffer, mask), index);

            return item;
        }

        private T[] GetNextBuffer(T[] buffer, long mask)
        {
            long nextArrayOffset = NextArrayOffset(mask);
            T[] nextBuffer = buffer[nextArrayOffset] as T[];

            buffer[nextArrayOffset] = default(T);
            return nextBuffer;
        }

        private long NextArrayOffset(long mask)
        {
            return ModifiedCalcElementOffset(mask + 2, long.MaxValue);
        }

        private T NewBufferPoll(T[] nextBuffer, long index)
        {
            long offsetInNew = NewBufferAndOffset(nextBuffer, index);
            T item = nextBuffer[offsetInNew];

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new InvalidOperationException("new buffer must have at least one element");

            nextBuffer[offsetInNew] = default(T);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);

            return item;
        }

        private T NewBufferPeek(T[] nextBuffer, long index)
        {
            long offsetInNew = NewBufferAndOffset(nextBuffer, index);
            T item = nextBuffer[offsetInNew];

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new InvalidOperationException("new buffer must have at least on element");

            return item;
        }

        private long NewBufferAndOffset(T[] nextBuffer, long index)
        {
            consumerBuffer = nextBuffer;
            consumerMask = (nextBuffer.Length - 2L) << 1;
            long offsetInNew = ModifiedCalcElementOffset(index, consumerMask);
            return offsetInNew;
        }

        public int Size()
        {
            long after = base.consumerIndex;
            long size;

            while (true)
            {
                long before = after;
                long currentProducerIndex = base.producerIndex;
                after = base.consumerIndex;
                if (before == after)
                {
                    size = ((currentProducerIndex - after) >> 1);
                    break;
                }
            }

            if (size > int.MaxValue)
                return int.MaxValue;
            else
                return (int)size;
        }

        public bool RelaxedOffer(T item)
        {
            return Offer(item);
        }

        private bool CasProducerLimit(long expect, long newValue)
        {
            return Interlocked.CompareExchange(ref base.producerLimit, newValue, expect) == expect;
        }

        public long CurrentProducerIndex()
        {
            return base.producerIndex / 2;
        }

        public long CurrentConsumerIndex()
        {
            return base.consumerIndex / 2;
        }

        public bool IsEmpty
        {
            // order matters!
            // loading consumer before producer allows for producer incremeents after consumer index is read.
            // this ensures this method is conservative in it's estimate. Note that as this is an MPMC there
            // is nothing we can do to make this an exact method.
            get { return base.consumerIndex == base.producerIndex; }
        }

        public T RelaxedPoll()
        {
            T[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset];

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                return item;

            if (EqualityComparer<T>.Default.Equals(item, JUMP))
            {
                T[] nextBuffer = GetNextBuffer(buffer, mask);
                return NewBufferPoll(nextBuffer, index);
            }

            buffer[offset] = default(T);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);
            return item;
        }

        public T RelaxedPeek()
        {
            T[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset];

            if (EqualityComparer<T>.Default.Equals(item, JUMP))
                return NewBufferPeek(GetNextBuffer(buffer, mask), index);

            return item;
        }

        private void Resize(long oldMask, T[] oldBuffer, long pIndex, T e)
        {
            int newBufferLength = GetNextBufferSize(oldBuffer);
            T[] newBuffer = new T[newBufferLength];

            producerBuffer = newBuffer;
            int newMask = (newBufferLength - 2) << 1;
            producerMask = newMask;

            long offsetInOld = ModifiedCalcElementOffset(pIndex, oldMask);
            long offsetInNew = ModifiedCalcElementOffset(pIndex, newMask);

            newBuffer[offsetInNew] = e;
            oldBuffer[NextArrayOffset(oldMask)] = ((object)newBuffer) as T;
            //soElement(newBuffer, offsetInNew, e);
            //soElement(oldBuffer, NextArrayOffset(oldMask), newBuffer);

            long cIndex = base.consumerIndex;
            long availableInQueue = AvailableInQueue(pIndex, cIndex);
            if (availableInQueue <= 0)
                throw new InvalidOperationException();

            Interlocked.Exchange(ref base.producerLimit, (int)(pIndex + Math.Min(newMask, availableInQueue)));

            // make resize visible other the other producers.
            Interlocked.Exchange(ref base.producerIndex, pIndex + 2);

            // INDEX visible before ELEMENT< consistent with consumer expectation

            // make resize visible to consumer.
        }

        public abstract int Capacity { get; }

        protected abstract int GetNextBufferSize(T[] buffer);

        protected abstract long GetCurrentBufferCapacity(long mask);
    }
}
