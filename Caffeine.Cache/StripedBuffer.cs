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
using System.Threading;

namespace Caffeine.Cache
{
    /// <summary>
    /// A base class providing mechanics for supporting dynamic striping of bounded buffers.
    /// This implementation is an adaptation of the numeric 64-bit (Java Striped64) class,
    /// which is used by atomic counteres. The approach was modified to lazily grow an
    /// array of buffers in order to minimize memory usage for caches that are not
    /// heavily contended on.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class StripedBuffer<T> : Buffer<T>
    {
        /*
         * This class maintains a lazily-initialized table of atomically updated buffers. The
         * table size is a power of two. Indexing uses masked per-thread hash codes. Nearly all
         * declarations in this class are package-private, accessed directly by subclasses.
         * 
         * Table entries are of class Buffer and should be padded to reduce cache contention.
         * Padding is overkill for most atomics because they are usually irregularly scattered
         * in memory and thus don't interfere much with each other. But atomic objects residing
         * in arrays will tend to be placed adjacent to each other, and so will most often share
         * cache lines (with a huge negative performance impact) without this protection.
         * 
         * In part because Buffers are relatively large, we avoid creating them until they are 
         * needed. When there is no contention, all updates are made to a single buffer. Upon
         * contention (a failed CAS inserting into the buffer), the table is expanded to size 2.
         * The table size is doubled upon further contention until reaching the nearest power of two
         * greater than or equal to the number of CPUs. Table slots remaing empty (null) until they
         * are needed.
         * 
         * Single lock (tableLock) is used for initializing and resizing the table, as well as populating
         * slots with new Buffers. There is no ndeed for a blocking lock; when the lock is not 
         * available, threads try other slots. During these retries, there is increased contention
         * and reduced locality, which is still better than alternatives.
         * 
         * The thread probe fields maintained serve as per-thread hash codes. We let them remain
         * uninitialized as zero (if they come in this way) until they contend at slot 0. They
         * are then initialized to values that typically do not often conflict with others. Contention
         * and/or table collisions are indicated by failed CASes when performing an update operation.
         * Upon a collision, if the table size is less than the capacity, it is doubled in size unless
         * some other thread holds the lock. If a hashed slot is empty, and lock is available, a new
         * Buffer is created. Otherwise, if the slot exists, a CAS is tried. Retries proceed by
         * "double hashing", using a secondary hash (Marsaglia XorShift) to try to find a free slot.
         * 
         * The table size is capped because, when there are more threads than CPUs, supposing that
         * each thread were bound to a CPU, there would exist a perfect hash function mapping threads
         * to slots that eliminates collisions. When we reach capacity, we search for this mapping by
         * randomly varying the hash codes of colliding threads. Because search is random, and collisions
         * only become known via CAS failures, convergence can be slow, and because threads are typically
         * not bound to CPUs forever, may not occur at all. However, despite these limitations, observed
         * contention rates are typically low in these cases.
         * 
         * It is possible for a Buffer to become unused when threads that once hashed to it terminate,
         * as well as in the case where doubling the table causes no thread to hash to it under
         * expanded mask. We do not try to detect or remove buffers, under the assumption that for
         * long-running instances, observed contention levels will recur, so the buffers will eventually
         * be needed again; and for short-lived ones, it does not matter.
         * 
         */

        // the bound on the table size.
        private static readonly int MAXIMUM_TABLE_SIZE = 4 * Utility.CeilingNextPowerOfTwo(Environment.ProcessorCount);

        // the maximum number of attempts when trying to expand the table.
        private static readonly int MAX_ATTEMPTS = 3;

        // table of buffers. When non-null, size is a power of 2.
        private Buffer<T>[] table;

        // lock used when resizing and/or creating the table.
        private static volatile object tableLock = new object();

        private readonly AsyncLocal<Random> random = new AsyncLocal<Random>() { Value = new Random(GetSeed()) };
        private AsyncLocal<long> probe = new AsyncLocal<long>();

        public StripedBuffer()
        { }

        // TODO: I'm not sure what was going on here in the original code.. but I think we can omit with our version of Random generator.
        //private static long AdvanceProbe(long probe)
        //{
        //    probe ^= probe << 13;
        //    probe ^= probe >> 17;
        //    probe ^= probe << 5;

        //    return probe;
        //}

        private long AdvanceProbe()
        {
            probe.Value = random.Value.Next();
            return probe.Value;
        }

        /// <summary>
        /// Creates a new buffer instance after reisizing to accomodate a producer.
        /// </summary>
        /// <param name="element">the producer's element</param>
        /// <returns>A newly created buffer populated with a single element</returns>
        protected abstract Buffer<T> Create(T element);

        public override OfferStatusCodes Offer(T element)
        {
            OfferStatusCodes result = 0;

            // TODO: Why bother assigning to this other variable because it's still just a reference to the original Array.
            Buffer<T>[] buffers = table;
            
            int mask = buffers.Length - 1;
            Buffer<T> buffer = buffers[probe.Value & mask];

            bool uncontended = true;

            if ((buffers == null) || (mask < 0) || (buffer == null))
            {
                ExpandOrRetry(element, uncontended);
            }
            else
            {
                result = buffer.Offer(element);
                uncontended = (result != OfferStatusCodes.FAILED);

                if (!uncontended)
                {
                    ExpandOrRetry(element, uncontended);
                }
            }

            return result;
        }

        public override void DrainTo(Action<T> consumer)
        {
            Buffer<T>[] buffers = table;

            if (buffers == null)
                return;

            foreach (Buffer<T> buffer in buffers)
            {
                if (buffer != null)
                    buffer.DrainTo(consumer);
            }
        }

        public override uint Reads()
        {
            Buffer<T>[] buffers = table;

            if (buffers == null || buffers.Length == 0)
                return 0;

            uint reads = 0;
            foreach (Buffer<T> buffer in buffers)
            {
                if (buffer != null)
                    reads += buffer.Reads();
            }

            return reads;
        }

        public override uint Writes()
        {
            Buffer<T>[] buffers = table;
            if (buffers == null)
                return 0;

            uint writes = 0;
            foreach (Buffer<T> buffer in buffers)
            {
                if (buffer != null)
                    writes += buffer.Writes();
            }

            return writes;
        }

        /// <summary>
        /// Handles cases of updates involving intialization, resizing, creating new buffers
        /// and/or contention. See above for explanation. This method suffers the usual
        /// non-modularity problems of optimistic retry code, relying on rechecked sets of reads.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="wasUncontended"></param>
        private void ExpandOrRetry(T element, bool wasUncontended)
        {
            long h = 0;

            bool collide = false; // true if last slot is not empty.

            h = AdvanceProbe();

            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                Buffer<T>[] buffers = table;
                Buffer<T> buffer;

                int bufferLength = buffers.Length;

                if ((buffers != null) && (bufferLength > 0))
                {
                    if ((buffer = buffers[(bufferLength - 1) & h]) == null)
                    {
                        bool created = false;

                        lock (tableLock)
                        {
                            Buffer<T>[] rs = table;
                            long mask = rs.Length;
                            long j = (mask - 1) & h;
                            if ((rs != null) && (mask > 0) && (rs[j] == null))
                            {
                                rs[j] = Create(element);
                                created = true;
                            }
                        }

                        if (created)
                            break;

                        collide = false;
                    }
                    else if (buffer.Offer(element) != OfferStatusCodes.FAILED)
                    {
                        break;
                    }
                    else if (bufferLength >= MAXIMUM_TABLE_SIZE || table != buffers)
                    {
                        collide = false;
                    }
                    else if (!collide)
                    {
                        collide = true;
                    }
                    else
                    {
                        lock (tableLock)
                        {
                            if (table == buffers)
                            {
                                Buffer<T>[] newTable = new Buffer<T>[bufferLength << 1];
                                Buffer.BlockCopy(table, 0, newTable, 0, bufferLength);
                                table = newTable;
                            }

                            collide = false;
                        }
                        continue;
                    }
                    h = AdvanceProbe();
                }
                else
                {
                    bool init = false;

                    lock (tableLock)
                    {
                        if (table == buffers)
                        {
                            Buffer<T>[] rs = new Buffer<T>[1];
                            rs[0] = Create(element);
                            table = rs;
                            init = true;
                        }
                    }

                    if (init)
                        break;
                }
            }
        }

        private static int GetSeed()
        {
            return Environment.TickCount * Thread.CurrentThread.ManagedThreadId;
        }
    }
}
