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
    public sealed class RingBuffer<T> : ReadAndWriteCounterRef<T> where T : class
    {
        // TODO: this is just putting spacing between the elements of the buffer. We also need to add padding, because C# Array has metadata at the beginning the checks array bounds.

        // the maximum number of elements per buffer.
        private static int BUFFER_SIZE = 16;

        private static readonly int SPACED_SIZE = BUFFER_SIZE << 4;
        private static readonly int SPACED_MASK = SPACED_SIZE - 1;
        private static readonly int OFFSET = 16;

        // TODO: java version used AtomicReferenceArray.. best I can figure is that we should just use Array in C#.
        private readonly T[] buffer;

        public RingBuffer(T element)
        {
            buffer = new T[SPACED_SIZE];
            Interlocked.Exchange<T>(ref buffer[0], element);

            // TODO: this was in the constructor for ReadAndWriteCounterRef, but seemed more appropriate here due to the accessibility of OFFSET
            Interlocked.Exchange(ref WriteCounter, OFFSET);
            Interlocked.Exchange(ref ReadCounter, 0);
        }

        public override OfferStatusCodes Offer(T element)
        {
            OfferStatusCodes rval = OfferStatusCodes.SUCCESS;

            long head = ReadCounter;
            long tail = WriteCounter;
            long size = (tail - head);

            if (size >= SPACED_SIZE)
                return OfferStatusCodes.FULL;

            if (Interlocked.CompareExchange(ref WriteCounter, tail + OFFSET, tail) == tail)
            {
                int index = (int)(tail & SPACED_MASK);
                Interlocked.Exchange(ref buffer[index], element);
                rval = OfferStatusCodes.SUCCESS;
            }
            else
                rval = OfferStatusCodes.FAILED;


            // TODO: a bit of a refactor to get rid of multiple return statements through the function.
            return rval;
        }

        public override void DrainTo(Action<T> consumer)
        {
            long head = ReadCounter;
            long tail = WriteCounter;
            long size = (tail - head);

            if (size == 0)
                return;

            do
            {
                int index = (int)(head & SPACED_MASK);
                T element = buffer[index];
                if (element == null)
                    break;

                Interlocked.Exchange<T>(ref buffer[0], null);
                consumer(element);
                head += OFFSET;

            } while (head != tail);

            Interlocked.Exchange(ref ReadCounter, head);
        }

        public override uint Reads()
        {
            return (uint)(ReadCounter / OFFSET);
        }

        public override uint Writes()
        {
            return (uint)(WriteCounter / OFFSET);
        }
    }

    public abstract class PadReadCounter<T1> : Buffer<T1>
    {
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p10, p11, p12, p13, p14, p15, p16;
    }

    public abstract class ReadCounterRef<T2> : PadReadCounter<T2>
    {
        protected long ReadCounter;
    }

    public abstract class PadWriteCounter<T3> : ReadCounterRef<T3>
    {
        long p20, p21, p22, p23, p24, p25, p26, p27;
        long p30, p31, p32, p33, p34, p35, p36;
    }

    public abstract class ReadAndWriteCounterRef<T4> : PadWriteCounter<T4>
    {
        protected long WriteCounter;
    }
}
