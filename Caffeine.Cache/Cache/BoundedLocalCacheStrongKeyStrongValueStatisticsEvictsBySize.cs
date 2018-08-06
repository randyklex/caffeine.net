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
using System.Threading.Tasks;
using Caffeine.Cache.MpscQueue;

namespace Caffeine.Cache
{
    /// <summary>
    /// A cache that provides the following features;
    /// <list type="bullet">
    /// <item><description>MaximumSize</description></item>
    /// <item><description>StrongKeys (inherited)</description></item>
    /// <item><description>StrongValues (inherited)</description></item>
    /// <item><description>Statistics (inherited)</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class BoundedLocalCacheStrongKeyStrongValueStatisticsEvictsBySize<K, V> : BoundedLocalCacheStrongKeyStrongValueStatistics<K, V>
    {
        private ulong maximum;
        private ulong edenMaximum;
        private ulong mainProtectedMaximum;
        private ulong weightedSize;
        private ulong edenWeightedSize;
        private ulong mainProtectedWeightedSize;

        private readonly AccessOrderDeque<Node<K, V>> accessOrderEdenDeque;
        private readonly AccessOrderDeque<Node<K, V>> accessOrderProbationDeque;
        private readonly AccessOrderDeque<Node<K, V>> accessOrderProtectedQueue;

        private readonly MpscQueue.MpscGrowableArrayQueue<Task> writeBuffer;

        private readonly FrequencySketch<K> sketch;

        public BoundedLocalCacheStrongKeyStrongValueStatisticsEvictsBySize(Caffeine<K, V> builder, CacheLoader<K, V> loader, bool isAsync)
            : base(builder, loader, isAsync)
        {
            sketch = new FrequencySketch<K>();
            if (builder.HasInitialCapacity)
            {
                long capacity = Math.Min(builder.Maximum, builder.InitialCapacity);
                sketch.EnsureCapacity((ulong)capacity);
            }

            accessOrderEdenDeque = builder.Evicts || builder.DoesExpireAfterAccess ? new AccessOrderDeque<Node<K, V>>() : null;
            accessOrderProbationDeque = new AccessOrderDeque<Node<K, V>>();
            accessOrderProtectedQueue = new AccessOrderDeque<Node<K, V>>();

            this.writeBuffer = new MpscGrowableArrayQueue<Task>(WRITE_BUFFER_MIN, WRITE_BUFFER_MAX);
        }

        internal override bool Evicts
        {
            get { return true; }
        }

        public override ulong Maximum
        {
            get { return maximum; }
            protected set { maximum = value; }
        }

        protected override ulong EdenMaximum
        {
            get { return edenMaximum; }
            set { edenMaximum = value; }
        }

        protected override ulong MainProtectedMaximum
        {
            get { return mainProtectedMaximum; }
            set { mainProtectedMaximum = value; }
        }

        protected override ulong WeightedSize
        {
            get { return weightedSize; }
            set { weightedSize = value; }
        }

        protected override ulong EdenWeightedSize
        {
            get { return edenWeightedSize; }
            set { edenWeightedSize = value; }
        }

        protected override ulong MainProtectedWeightedSize
        {
            get { return mainProtectedWeightedSize; }
            set { mainProtectedWeightedSize = value; }
        }

        internal override AccessOrderDeque<Node<K, V>> AccessOrderEdenDeque
        {
            get { return accessOrderEdenDeque; }
        }

        internal override AccessOrderDeque<Node<K, V>> AccessOrderProbationDeque
        {
            get { return accessOrderProbationDeque; }
        }

        internal override AccessOrderDeque<Node<K, V>> AccessOrderProtectedDeque
        {
            get { return AccessOrderProtectedDeque; }
        }

        protected override FrequencySketch<K> FrequencySketch
        {
            get { return sketch; }
        }

        protected sealed override bool BuffersWrites
        {
            get { return true; }
        }

        internal override MpscGrowableArrayQueue<Task> WriteBuffer
        {
            get { return writeBuffer; }
        }

        protected override bool FastPath
        {
            get { return true; }
        }
    }
}
