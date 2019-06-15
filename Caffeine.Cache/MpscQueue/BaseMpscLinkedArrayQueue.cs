/**
 * An MPSC array queue which starts at <i>initialCapacity</i> and grows to <i>maxCapacity</i> in
 * linked chunks of the initial size. The queue grows only when the current buffer is full and
 * elements are not copied on resize, instead a link to the new buffer is stored in the old buffer
 * for the consumer to follow.<br>
 * <p>
 * This is a shaded copy of <tt>MpscGrowableArrayQueue</tt> provided by
 * <a href="https://github.com/JCTools/JCTools">JCTools</a> from version 2.0.
 *
 * @author nitsanw@yahoo.com (Nitsan Wakart)
 */

using System;
using System.Collections.Generic;
using System.Threading;

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache.MpscQueue
{
    internal abstract class BaseMpscLinkedArrayQueue<T> : BaseMpscLinkedArrayQueueColdProducerFields<T>, IQueue<T>
    {
        private readonly static object JUMP = new object();
        //private readonly int REF_ELEMENT_SHIFT = IntPtr.Size == 4 ? 2 : 3;

        private const int RETRY = 1;
        private const int FULL = 2;
        private const int RESIZE = 3;

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
            (T Item, object NextBuffer)[] buffer = new (T Item, object NextBuffer)[p2capacity + 1];

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

        // TODO: Changed from Offer as in the Java version to match .NET semantics
        public bool Enqueue(T item)
        {
            // TODO: Removed the Default(T) check.. Because I want this queue to be usable with Value Types.
            // and if you're a value type like int, a default(int) is 0, which someone might want to enqueue.
            // NOTE: this is consistent with BCL generics.. you can add NULL items to a list.

            long mask;
            long pIndex;
            (T, object)[] buffer;

            while (true)
            {
                long producerLimit = base.producerLimit;
                pIndex = base.producerIndex;

                // if resizing, spin till it's no longer resizing.
                if (IsResizing(pIndex))
                    continue;

                // mask/buffer may get changed by resizing
                // pnly use for array access after successful CAS.
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
                        case RETRY:
                            continue;
                        case FULL:
                            return false;
                        case RESIZE:
                            Resize(mask, buffer, pIndex, item);
                            return true;
                    }
                }

                if (CasProducerIndex(pIndex, pIndex + 2))
                    break;
            }

            // index visible before element, consistent with consumer expectation.
            long offSet = ModifiedCalcElementOffset(pIndex, mask);
            buffer[offSet] = (item, null);
            return true;
        }

        private bool IsResizing(long pIndex)
        {
            // lower bit is indicative of resize.
            return ((pIndex & 1) == 1);
        }

        private bool CasProducerIndex(long expect, long newValue)
        {
            return Interlocked.CompareExchange(ref base.producerIndex, newValue, expect) == expect;
        }

        private long ModifiedCalcElementOffset(long index, long mask)
        {
            return (index & mask) >> 1;
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
                    result = RETRY; // retry from top.
            }
            // full and cannot grow
            else if (AvailableInQueue(pIndex, cIndex) <= 0)
            {
                result = FULL; // return false;
            }
            // grab index for resize -> set lower bit
            else if (CasProducerIndex(pIndex, pIndex + 1))
            {
                result = RESIZE; // resize
            }
            else
                result = RETRY; // failed resize attempt, retry from top.

            return result;
        }

        /// <summary>
        /// This impelmentation is correct for single consumer thread use only.
        /// </summary>
        /// <returns></returns>
        // TODO: Changed from Poll to Dequeue to be inline with .NET nomenclature
        public T Dequeue()
        {
            (T Item, object NextBuffer)[] buffer = consumerBuffer;
            long index = consumerIndex;
            long mask = consumerMask;

            long offSet = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offSet].Item;
            object nextBufferPtr = buffer[offSet].NextBuffer;

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
            {
                if (index != base.producerIndex)
                {
                    do
                    {
                        item = buffer[offSet].Item;

                    } while (EqualityComparer<T>.Default.Equals(item, default(T)));
                }
                else
                {
                    return default(T);
                }
            }

            if (EqualityComparer<object>.Default.Equals(nextBufferPtr, JUMP))
            {
                (T Item, object NextBuffer)[] nextBuffer = GetNextBuffer(buffer, mask);
                return NewBufferDequeue(nextBuffer, index);
            }

            buffer[offSet] = (default(T), null);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);

            return item;
        }

        /// <summary>
        /// This impelmentation is correct for single consumer thread use only.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            (T Item, object NextBuffer)[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset].Item;
            object nextBuffer = buffer[offset].NextBuffer;
 
            if (EqualityComparer<T>.Default.Equals(item, default(T)) && index != base.producerIndex)
            {
                while (EqualityComparer<T>.Default.Equals((item = buffer[offset].Item), default(T)))
                {
                    ;
                }
            }

            if (EqualityComparer<object>.Default.Equals(nextBuffer, JUMP))
                return NewBufferPeek(GetNextBuffer(buffer, mask), index);

            return item;
        }

        private (T Item, object NextBuffer)[] GetNextBuffer((T Item, object NextBuffer)[] buffer, long mask)
        {
            long nextArrayOffset = NextArrayOffset(mask);
            (T Item, object NextBuffer)[] nextBuffer = buffer[nextArrayOffset] as (T Item, object NextBuffer)[];

            buffer[nextArrayOffset] = (default(T), null);
            return nextBuffer;
        }

        private long NextArrayOffset(long mask)
        {
            return ModifiedCalcElementOffset(mask + 2, long.MaxValue);
        }

        private T NewBufferDequeue((T Item, object NextBuffer)[] nextBuffer, long index)
        {
            long offsetInNew = NewBufferAndOffset(nextBuffer, index);
            T item = nextBuffer[offsetInNew].Item;

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new InvalidOperationException("new buffer must have at least one element");

            nextBuffer[offsetInNew] = (default(T), null);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);

            return item;
        }

        private T NewBufferPeek((T Item, object NextBuffer)[] nextBuffer, long index)
        {
            long offsetInNew = NewBufferAndOffset(nextBuffer, index);
            T item = nextBuffer[offsetInNew].Item;

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new InvalidOperationException("new buffer must have at least on element");

            return item;
        }

        private long NewBufferAndOffset((T Item, object NextBuffer)[] nextBuffer, long index)
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
            return Enqueue(item);
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

        public T RelaxedDequeue()
        {
            (T Item, object NextBuffer)[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset].Item;
            object nextBufferPtr = buffer[offset].NextBuffer;

            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                return item;

            if (EqualityComparer<object>.Default.Equals(nextBufferPtr, JUMP))
            {
                (T Item, object NextBuffer)[] nextBuffer = GetNextBuffer(buffer, mask);
                return NewBufferDequeue(nextBuffer, index);
            }

            buffer[offset] = (default(T), null);
            Interlocked.Exchange(ref base.consumerIndex, index + 2);
            return item;
        }

        public T RelaxedPeek()
        {
            (T Item, object NextBuffer)[] buffer = consumerBuffer;
            long index = base.consumerIndex;
            long mask = base.consumerMask;

            long offset = ModifiedCalcElementOffset(index, mask);
            T item = buffer[offset].Item;
            object nextBufferPtr = buffer[offset].NextBuffer;

            if (EqualityComparer<object>.Default.Equals(nextBufferPtr, JUMP))
                return NewBufferPeek(GetNextBuffer(buffer, mask), index);

            return item;
        }

        private void Resize(long oldMask, (T Item, object NextBuffer)[] oldBuffer, long pIndex, T e)
        {
            int newBufferLength = GetNextBufferSize(oldBuffer);
            (T Item, object NextBuffer)[] newBuffer = new (T Item, object NextBuffer)[newBufferLength];

            producerBuffer = newBuffer;
            int newMask = (newBufferLength - 2) << 1;
            producerMask = newMask;

            long offsetInOld = ModifiedCalcElementOffset(pIndex, oldMask);
            long offsetInNew = ModifiedCalcElementOffset(pIndex, newMask);

            newBuffer[offsetInNew].Item = e;
            oldBuffer[NextArrayOffset(oldMask)].NextBuffer = newBuffer;
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
            oldBuffer[offsetInOld].NextBuffer = JUMP;
        }

        public abstract int Capacity { get; }

        protected abstract int GetNextBufferSize((T Item, object NextBuffer)[] buffer);

        protected abstract long GetCurrentBufferCapacity(long mask);

        /// <summary>
        /// Available elements in queue * 2.
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="cIndex"></param>
        /// <returns></returns>
        protected abstract long AvailableInQueue(long pIndex, long cIndex);
    }
}
