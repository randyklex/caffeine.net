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
    internal abstract class MpscChunkedArrayQueueColdProducerFields<T> : BaseMpscLinkedArrayQueue<T>
    {
        protected readonly long maxQueueCapacity;

        public MpscChunkedArrayQueueColdProducerFields(int initialCapacity, int maxCapacity)
            : base(initialCapacity)
        {
            if (maxCapacity < 4)
                throw new ArgumentException("Max capacity must be 4 or more.", "maxCapacity");

            if (Utility.CeilingNextPowerOfTwo(initialCapacity) >= Utility.CeilingNextPowerOfTwo(maxCapacity))
                throw new ArgumentException("Initial capacity cannot exceed maximum capacity (both founded up to a power of 2", "initialCapacity");

            maxQueueCapacity = ((long)Utility.CeilingNextPowerOfTwo(maxCapacity)) << 1;
        }


    }
}
