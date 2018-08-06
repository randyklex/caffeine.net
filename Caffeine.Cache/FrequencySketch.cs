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
    /// A probabilistic multiset for estimating the popularity of an element within a time window.
    /// The maximum frequency of an element is limisted to 15 (4-bits) and an aging process
    /// periodically halves the popularity of all elements.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class FrequencySketch<T>
    {
        /*
         * This class maintains a 4-bit CountMinSketch [1] with periodic aging to provide the popularity
         * history for the TinyLfu admission policy [2]. The time and space efficiency of the sketch
         * allows it to cheaply estimate the frequency of an entry in a stream of cache access events.
         * 
         * The counter matrix is represented as a single dimensional array holding 16 counters per slot.
         * A fixed depth of four blanaces the accuracy and cost, resulting in a width of four times
         * the lenght of the array. To retain an accurate estimation the array's length equals the maximum
         * number of entries in the cache, increased to the closest power-of-two to exploit more efficient
         * bit masking. This configuration results in a confidence of 93.75% and error bound of e / width.
         * 
         * The frequency of all etnries is aged periodically using a sampling window based on the maximum
         * number of entries in the cache. This is referred to as the reset operation by TinyLfu and keeps
         * the sketch fresh by dividing all counters by two and subtracting based on the number of odd
         * counters found. The O(n) cost of aging is amortized,  ideal for hardware prefetching, and uses
         * inexpensive bit manitpulations per array location.
         * 
         * A per instance smear is used to help protect against hash flooding [3], which would result
         * in the admission policy always rejecting new candidates. The use of a pseudo random hashing
         * function resolves the concern of a denial of service attack by exploiting the hash codes.
         * 
         * [1] An Improved Data Stream Summary: the Count-Min Sketch and its Applications
         * http://dimacs.rutgers.edu/~graham/pubs/papers/cm-full.pdf
         * [2] TinyLFU: A Highly Efficient Cache Admission Policy
         * http://arxiv.org/pdf/1512.00727.pdf
         * [3] Denial of Service via Algorithmic Complexity Attack
         * https://www.usenix.org/legacy/events/sec03/tech/full_papers/crosby/crosby.pdf
         * 
         */

        // a mixture of seeds from FNV-1A, CityHash and Murmur3
        static readonly ulong[] SEED = new ulong[] { 0xc3a5c85c97cb3127, 0xb492b66fbe98f273, 0x9ae16a3b2f90404f, 0xcbf29ce484222325 };

        static readonly ulong RESET_MASK = 0x7777777777777777;
        static readonly ulong ONE_MASK = 0x1111111111111111;

        private readonly int randomSeed;

        private int sampleSize;
        private int tableMask;
        private ulong[] table;
        private uint size;

        private readonly AsyncLocal<Random> random = new AsyncLocal<Random>() { Value = new Random(GetSeed()) };

        static int GetSeed()
        {
            return Environment.TickCount * Thread.CurrentThread.ManagedThreadId;
        }

        public FrequencySketch()
        {
            randomSeed = 1 | random.Value.Next();
        }

        /// <summary>
        /// Initializes and if necessary, increases the capacity of this <see cref="FrequencySketch{T}"/> instance to
        /// ensure that it can accurately estimate the popularity of elements given the maximumSize
        /// of the cache. This operation forgets all previous counts when resizing.
        /// </summary>
        /// <param name="maximumSize">The maximum size of the cache</param>
        public void EnsureCapacity(ulong maximumSize)
        {
            int maximum = (int)Math.Min(maximumSize, uint.MaxValue >> 1);

            if ((table != null) && table.Length >= maximum)
                return;

            table = new ulong[(maximum == 0) ? 1 : Utility.CeilingNextPowerOfTwo(maximum)];
            tableMask = Math.Max(0, table.Length - 1);
            sampleSize = (maximumSize == 0) ? 10 : (10 * maximum);

            if (sampleSize <= 0)
                sampleSize = int.MaxValue;

            size = 0;
        }

        /// <summary>
        /// If the Sketch has not be initialized, returns true, requiring that EnsureCapacity
        /// is called before it begins to track frequencies.
        /// </summary>
        /// <returns></returns>
        public bool IsNotInitialized
        {
            get { return (table == null); }
        }

        /// <summary>
        /// Returns the estimated number of occurrences of an element, up to the maximum (15).
        /// </summary>
        /// <param name="element">The element to count occurrences of.</param>
        /// <returns>The estimated number of occurrences of the element; possibly zero but never negative.</returns>
        public int Frequency(T element)
        {
            if (IsNotInitialized)
                return 0;

            int hash = Spread(element.GetHashCode());
            int start = (hash & 3) << 2;
            int frequency = Int32.MaxValue;

            // TODO: magic number.. can we replace "4" with a human word to describe what it means?
            for (int i = 0; i < 4; i++)
            {
                int index = IndexOf(hash, i);
                int count = (int)((table[index] >> ((start + i) << 2)) & 0xf);
                frequency = Math.Min(frequency, count);
            }

            return frequency;
        }

        /// <summary>
        /// Increments the popularity of the element if it does not exceed the maximum (15). The popularity
        /// of all elements will be periodically down sampled when the observed events exceeds a 
        /// threshold. This process provides a frequency aging to allow expired long term entries to 
        /// fade away.
        /// </summary>
        /// <param name="element">The element to add.</param>
        public void Increment(T element)
        {
            if (IsNotInitialized)
                return;

            int hash = Spread(element.GetHashCode());
            int start = (hash & 3) << 2;

            int index0 = IndexOf(hash, 0);
            int index1 = IndexOf(hash, 1);
            int index2 = IndexOf(hash, 2);
            int index3 = IndexOf(hash, 3);

            bool added = IncrementAt(index0, start);
            added |= IncrementAt(index1, start + 1);
            added |= IncrementAt(index2, start + 2);
            added |= IncrementAt(index3, start + 3);

            if (added && (++size == sampleSize))
                Reset();
        }

        /// <summary>
        /// Increments the specified counter by 1 if it is not already at the maximum value (15).
        /// </summary>
        /// <param name="tableIndex">the table index</param>
        /// <param name="counter">the counter to increment.</param>
        /// <returns>True if incremented</returns>
        private bool IncrementAt(int tableIndex, int counter)
        {
            int offset = counter << 2;

            ulong mask = (0xFUL << offset);
            if ((table[tableIndex] & mask) != mask)
            {
                table[tableIndex] += (1UL << offset);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reduces every counter by half of its original value.
        /// </summary>
        private void Reset()
        {
            uint count = 0;
            for (int i=0; i< table.Length; i++)
            {
                count += Utility.NumberOfSetBits((ulong)(table[i] & ONE_MASK));
                table[i] = (table[i] >> 1) & RESET_MASK;
            }

            size = (size >> 1) - (count >> 2);
        }

        /// <summary>
        /// Returns the table index for the counter at the specified depth
        /// </summary>
        /// <param name="item">the element's hash.</param>
        /// <param name="i">the counter depth</param>
        /// <returns>the table index</returns>
        private int IndexOf(int itemHash, int counterDepth)
        {
            ulong hash = SEED[counterDepth] * (ulong)itemHash;
            hash += hash >> 32;
            return ((int)hash) & tableMask;
        }

        /// <summary>
        /// Applies a suplemental hash function to a given hashCode which defends against
        /// poor quality hash functions.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private int Spread(int x)
        {
            x = ((x >> 16) ^ x) * 0x45d9f3b;
            x = ((x >> 16) ^ x) * randomSeed;
            return (x >> 16) ^ x;
        }
    }
}
