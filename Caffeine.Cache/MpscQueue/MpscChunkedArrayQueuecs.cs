﻿/*
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
using System.Text;

namespace Caffeine.Cache.MpscQueue
{
    internal abstract class MpscChunkedArrayQueuecs<T> : MpscChunkedArrayQueueColdProducerFields<T>
    {
        #pragma warning disable CS0169
        long po0, p1, p2, p3, p4, p5, p6, p7;
        long p10, p11, p12, p13, p14, p15, p16, p17;
        #pragma warning restore CS0169

        public MpscChunkedArrayQueuecs(int initialCapacity, int maxCapacity)
            : base(initialCapacity, maxCapacity)
        { }

        protected override long AvailableInQueue(long pindex, long cIndex)
        {
            return maxQueueCapacity - (pindex - cIndex);
        }

        public override int Capacity
        {
            get { return (int)(maxQueueCapacity / 2); }
        }

        protected override int GetNextBufferSize((T Item, object NextBuffer)[] buffer)
        {
            return buffer.Length;
        }

        protected override long GetCurrentBufferCapacity(long mask)
        {
            return mask;
        } 
    }
}
