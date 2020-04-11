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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using Caffeine.Cache.Factories;
using Caffeine.Cache.Interfaces;
using Caffeine.Cache.Stats;

namespace Caffeine.Cache
{
    /*
     * An in-memory cache implementation that supports full concurrency of retrievals,
     * a high expected concurrency for updates, and multiple ways to bound the cache.
     * 
     * this class is abstract and code generated subclasses provide the complete
     * implementation for a particular configuration. This is to ensure that only
     * the fields and execution paths necessary for a given configuration are used.
     * 
     */
    public class BoundedLocalCache<K, V> : DrainStatusRef<K, V>
    {
        /*
         * This class performs a best-effort bounding of a ConcurrentHashMap using
         * a page-replacement algorithm to determine which entries to evict when
         * the capacity is exceeded.
         * 
         * The page replacement algorithm's data structures are kept eventually
         * consistent with the map. An update to the map and recording of reads
         * may not be immediately reflected on the algorithm's data structures.
         * These structures are guarded by a lock and operations are applied in 
         * batches to avoid lock contention. The penalty of applying the batches
         * is spread across threads so that amortized cost is slightly higher than
         * performing just the ConcurrentHashMap operation.
         * 
         * A memento of the reads and writes that were performed on the map are recorded
         * in buffers. These buffers are drained at the first opportunity after a
         * write or when a read buffer is full. The reads are offered in a buffer that
         * will reject additions if contended on or if it is full and a strict policy
         * ordering is not possible, but is observably strict when single threaded.
         * The buffers are drained asynchronously to minimize the request latency
         * and uses a state machine to determine when to schedule a task.
         * 
         * Due to a lack of a strict ordering guarantee, a task can be executed
         * out-of-order, such as a removal followed by its addition. The state
         * of the entry is encoded using the key field to avoid additional memory.
         * An entry is "alive" if it is in both the hash table and the page
         * replacement policy. It is "retired" if it is not in the hash table
         * and is pending removal from the page replacement policy. Finally an entry
         * transitions to the "dead" state when it is not in the hash table nor the
         * page replacement policy. Both the retired and dead states are represented
         * by a sentinel key that should not be used for map lookups.
         * 
         * Expiration is implemented in O(1) time complexity. The time-to-idel policy
         * uses an access-order queue, the time-to-live poclify uses a write-order
         * queue and variable expiration uses a timer wheel. This allows peeking
         * at the oldest entry in the queue to see if it has expired and, if it
         * has not, then the younger entries must have not too. If a maximum size
         * is set then expiration will share the queues in order to minimize the
         * per-entry footprint. The expiration updates are applied in a best effort
         * fashion. The reording of variable or access-order expiration may be
         * discarded by the read buffer if full or contended on. Similarly the
         * reording of write expiration may be ignored for an entry if the last
         * update was within a short time window in order to avoid overwhelming the
         * write buffer.
         * 
         * Maximum size is implemented using the Window TinyLFU policy due to its 
         * high hit rate, O(1) time complexity, and small footprint. A new entry starts
         * in the eden space and remains there as long as it has high temporal
         * locality. Eventually an entry will slip from the eden space into the main
         * space. If the main space is already fully, then a historic frequency filter
         * determines whether to evict the newly admitted entry or the victim entry chosen
         * by main's space policy. The windowing allows the policy to have a high hit
         * rate when entries exhibit a bursty (high temporal, low frequency) access
         * pattern. The eden space uses LRU and the main space uses segmented LRU.
         * 
         */

        private static readonly int NUM_CPUS = Environment.ProcessorCount;

        // the initial capacity of the write buffer.
        protected static readonly int WRITE_BUFFER_MIN = 4;

        // the maximum capacity of the write buffer
        protected static readonly int WRITE_BUFFER_MAX = 128 * Utility.CeilingNextPowerOfTwo(NUM_CPUS);

        // the number of attempts to insert into the write buffer before yielding.
        private static readonly ushort WRITE_BUFFER_RETRIES = 100;

        // the maximum weighted capacity of the map
        private static readonly ulong MAXIMUM_CAPACITY = ulong.MaxValue - uint.MaxValue;

        // the percent of the maximum weighted capacity dedicated to the main space.
        private static readonly double PERCENT_MAIN = 0.99d;

        // the percent of the maximum weighted capacity dedicated to the main's protected space.
        private static readonly double PERCENT_MAIN_PROTECTED = 0.80d;

        // the maximum time window between entry updates before the expiration must be reordered.
        private static readonly long EXPIRE_WRITE_TOLERANCE = Utility.NANOSECONDS_IN_SECOND;

        // I wish i knew what unit of time MAX_EXPIRY is supposed to be..
        // right-shift 1 of long.MaxValue is 4,611,686,018,427,387,903
        // I can't come up with any units (sec, ms, micro, nanos) that equate that
        // to 150 years.. so I don't know what the hell is going on here..
        internal static readonly long MAXIMUM_EXPIRY = long.MaxValue >> 1; // 150 years.

        // TODO: I don't like taht this object internal to the class is being accessed by a bunch of places. It seems to violate the open-closed principal a bit??
        internal readonly ConcurrentDictionary<K, Node<K, V>> data;
        private readonly CacheLoader<K, V> cacheLoader;
        private readonly Action<Node<K, V>> accessPolicy;
        private readonly Buffer<Node<K, V>> readBuffer;

        // TODO: make this a property???
        internal readonly NodeFactory<K, V> nodeFactory;
        private readonly ICacheWriter<K, V> writer;
        private readonly IWeigher<K, V> weigher;

        internal readonly object evictionLock = new object();

        private bool isAsync;

        private readonly HashSet<K> keySet;
        private readonly ICollection<V> values;
        private readonly HashSet<KeyValuePair<K, V>> entries;

        private readonly AsyncLocal<Random> random = new AsyncLocal<Random>() { Value = new Random(GetSeed()) };

        static int GetSeed()
        {
            return Environment.TickCount * Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Constructs a <see cref="BoundedLocalCache{K, V}"/> with the properties specifed in the <paramref name="builder"/>.
        /// This constructor also defaults to a concurrency level of <code>NUM_CPUS * 2</code>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="cacheLoader"></param>
        /// <param name="isAsync"></param>
        internal BoundedLocalCache(Caffeine<K, V> builder, CacheLoader<K, V> cacheLoader, bool isAsync)
            : this(builder, cacheLoader, isAsync, NUM_CPUS * 2)
        { }

        internal BoundedLocalCache(Caffeine<K, V> builder, CacheLoader<K, V> cacheLoader, bool isAsync, int concurrencyLevel)
        {
            this.isAsync = isAsync;
            this.cacheLoader = cacheLoader;

            writer = builder.Writer;
            weigher = builder.Weigher;

            nodeFactory = NodeFactory<K, V>.NewFactory(builder, isAsync);

            if (Evicts | CollectKeys | CollectValues | ExpiresAfterAccess)
                readBuffer = new BoundedBuffer<Node<K, V>>();
            else
                readBuffer = DisabledBuffer<Node<K, V>>.Instance;

            if (Evicts || ExpiresAfterAccess)
                accessPolicy = OnAccess;
            else
                accessPolicy = (e) => { };

            // TODO: handle concurrentDictionary constructor for Concurrency..
            data = new ConcurrentDictionary<K, Node<K, V>>(concurrencyLevel, builder.InitialCapacity);

            if (Evicts)
                SetMaximum((ulong)builder.Maximum);
        }

        /// <summary>
        /// If the page replacement policy buffers writes.
        /// </summary>
        protected virtual bool BuffersWrites
        {
            get { return false; }
        }

        internal bool IsComputingAsync(Node<K, V> node)
        {
            // TODO: implement the async to check if a node is still calculating it's value.
            return false;
        }

        internal virtual AccessOrderDeque<Node<K, V>> AccessOrderEdenDeque
        {
            get { throw new NotSupportedException(); }
        }

        internal virtual AccessOrderDeque<Node<K, V>> AccessOrderProbationDeque
        {
            get { throw new NotSupportedException(); }
        }

        internal virtual AccessOrderDeque<Node<K, V>> AccessOrderProtectedDeque
        {
            get { throw new NotSupportedException(); }
        }

        internal virtual AccessOrderDeque<Node<K, V>> WriteOrderDeque
        {
            get { throw new NotSupportedException(); }
        }

        internal virtual MpscQueue.MpscGrowableArrayQueue<Task> WriteBuffer
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Returns whether this cache notifies a writer when an entry is modified.
        /// </summary>
        protected bool HasWriter
        {
            get { return writer != DisabledCacheWriter<K, V>.Instance; }
        }

        #region Stats Support

        // TODO: This was changed from a method in original source to a property in .NET.
        internal override bool IsRecordingStats
        {
            get { return false; }
        }

        public override IStatsCounter StatsCounter
        {
            get { return DisabledStatsCounter.Instance; }
        }

        internal override ITicker StatsTicker
        {
            get { return DisabledTicker.Instance; }
        }

        #endregion

        #region Reference Support

        // TODO: This was changed from a method in original source to a property in .NET.
        protected bool CollectKeys
        {
            get { return false; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        protected bool CollectValues
        {
            get { return false; }
        }

        #endregion

        #region Expiration Support

        // TODO: This was changed from a method in original source to a property in .NET.
        internal bool ExpiresVariable
        {
            get { return false; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        internal bool ExpiresAfterAccess
        {
            get { return false; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        internal long ExpiresAfterAccessNanos
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        // TODO: This was changed from a method in original source to a property in .NET.}
        internal bool ExpiresAfterWrite
        {
            get { return false; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        internal long ExpiresAfterWriteNanos
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        internal bool RefreshAfterWrite
        {
            get { return false; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        internal long RefreshAfterWriteNanos
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        public override bool HasWriteTime
        {
            get { return ExpiresAfterWrite || RefreshAfterWrite; }
        }

        // TODO: This was changed from a method in original source to a property in .NET.
        protected IExpiry<K, V> Expiry
        {
            get { return null; }
        }

        internal override ITicker ExpirationTicker
        {
            get { return DisabledTicker.Instance; }
        }

        protected TimerWheel<K, V> TimerWheel
        {
            get { throw new NotSupportedException(); }
        }

        #endregion

        #region Eviction Support

        internal virtual bool Evicts
        {
            get { return false; }
        }

        protected bool IsWeighted
        {
            get { return weigher != SingletonWeigher<K, V>.Instance; }
        }

        protected virtual FrequencySketch<K> FrequencySketch
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Returns if an access to an entry can skip notifying the eviction policy.
        /// </summary>
        protected virtual bool FastPath
        {
            get { return false; }
        }

        /// <summary>
        /// Returns the maximum weighted size.
        /// </summary>
        public virtual ulong Maximum
        {
            get { throw new NotSupportedException(); }
            protected set {; }
        }

        /// <summary>
        /// Returns the maximum weighted size of the main's protected space.
        /// </summary>
        protected virtual ulong EdenMaximum
        {
            get { throw new NotSupportedException(); }
            set {; }
        }

        protected virtual ulong MainProtectedMaximum
        {
            get { throw new NotSupportedException(); }
            set {; }
        }

        internal void SetMaximum(ulong maximum)
        {
            Contract.Requires(maximum >= 0);

            ulong max = Math.Min(maximum, MAXIMUM_CAPACITY);
            ulong eden = max - (ulong)(max * PERCENT_MAIN);
            ulong mainProtected = (ulong)((max - eden) * PERCENT_MAIN_PROTECTED);

            Maximum = max;
            EdenMaximum = eden;
            MainProtectedMaximum = mainProtected;

            if ((FrequencySketch != null) && !IsWeighted && WeightedSize >= (max >> 1))
            {
                FrequencySketch.EnsureCapacity(max);
            }
        }

        internal ulong AdjustedWeightedSize
        {
            get { return Math.Max(0, WeightedSize); }
        }

        protected virtual ulong WeightedSize
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected virtual ulong EdenWeightedSize
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        protected virtual ulong MainProtectedWeightedSize
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        void EvictEntries()
        {
            if (!Evicts)
                return;

            int candidates = EvictFromEden();
            EvictFromMain(candidates);
        }

        /// <summary>
        /// Evicts entries from the eden space into the main space while the eden size exceeds a maximum.
        /// </summary>
        /// <returns></returns>
        int EvictFromEdent()
        {
            int candidates = 0;

            lock (evictionLock)
            {
                Node<K, V> node = AccessOrderEdenDeque.Peek();
                while (EdenWeightedSize > EdenMaximum)
                {
                    if (node == null)
                        break;

                    Node<K, V> next = node.GetNextInAccessOrder();
                    if (node.Weight != 0)
                    {
                        node.MakeMainProbation();
                        AccessOrderEdenDeque.Remove(node);
                        AccessOrderProbationDeque.Add(node);
                        candidates++;

                        EdenWeightedSize = EdenWeightedSize - (ulong)node.PolicyWeight;
                    }

                    node = next;
                }
            }

            return candidates;
        }

        void EvictFromMain(int candidates)
        {
            int victimQueue = Node<K, V>.PROBATION;

            lock (evictionLock)
            {
                Node<K, V> victim = AccessOrderProbationDeque.PeekFirst();
                Node<K, V> candidate = AccessOrderProbationDeque.PeekLast();

                while (WeightedSize > Maximum)
                {

                    if (candidates == 0)
                        candidate = null;

                    if ((candidate == null) && (victim == null))
                    {
                        if (victimQueue == Node<K, V>.PROBATION)
                        {
                            victim = AccessOrderProtectedDeque.PeekFirst();
                            victimQueue = Node<K, V>.PROTECTED;
                            continue;
                        }
                        else if (victimQueue == Node<K, V>.PROTECTED)
                        {
                            victim = AccessOrderEdenDeque.PeekFirst();
                            victimQueue = Node<K, V>.EDEN;
                            continue;
                        }

                        break;
                    }

                    // skip over entries with zero weight.
                    if ((victim != null) && (victim.PolicyWeight == 0))
                    {
                        victim = victim.GetNextInAccessOrder();
                        continue;
                    }
                    else if ((candidate != null) && (candidate.PolicyWeight == 0))
                    {
                        candidate = candidate.GetPreviousInAccessOrder();
                        candidates--;
                        continue;
                    }

                    // Evict immediately if only one of the entries is present
                    if (victim == null)
                    {
                        Node<K, V> previous = candidate.GetPreviousInAccessOrder();
                        Node<K, V> evict = candidate;
                        candidate = previous;
                        candidates--;
                        EvictEntry(evict, RemovalCause.SIZE, 0L);
                        continue;
                    }
                    else if (candidate == null)
                    {
                        Node<K, V> evict = victim;
                        victim = victim.GetNextInAccessOrder();
                        EvictEntry(evict, RemovalCause.SIZE, 0L);
                        continue;
                    }

                    // Evict immediately if an entry was collected
                    K victimKey = victim.Key;
                    K candidateKey = candidate.Key;

                    if (victimKey == null)
                    {
                        Node<K, V> evict = victim;
                        victim = victim.GetNextInAccessOrder();
                        EvictEntry(evict, RemovalCause.COLLECTED, 0L);
                        continue;
                    }
                    else if (candidateKey == null)
                    {
                        candidates--;
                        Node<K, V> evict = candidate;
                        candidate = candidate.GetPreviousInAccessOrder();
                        EvictEntry(evict, RemovalCause.COLLECTED, 0L);
                        continue;
                    }

                    // evict immediately if the candidates weight exceeds the maximum
                    if ((ulong)candidate.PolicyWeight > Maximum)
                    {
                        candidates--;
                        Node<K, V> evict = candidate;
                        candidate = candidate.GetPreviousInAccessOrder();
                        EvictEntry(evict, RemovalCause.SIZE, 0L);
                        continue;
                    }

                    candidates--;
                    if (Admit(candidateKey, victimKey))
                    {
                        Node<K, V> evict = victim;
                        victim = victim.GetNextInAccessOrder();
                        EvictEntry(evict, RemovalCause.SIZE, 0L);
                        candidate = candidate.GetPreviousInAccessOrder();
                    }
                    else
                    {
                        Node<K, V> evict = candidate;
                        candidate = candidate.GetPreviousInAccessOrder();
                        EvictEntry(evict, RemovalCause.SIZE, 0L);
                    }
                }
            }
        }

        bool Admit(K candidateKey, K victimKey)
        {
            int victimFreq = FrequencySketch.Frequency(victimKey);
            int candidateFreq = FrequencySketch.Frequency(candidateKey);

            if (candidateFreq > victimFreq)
            {
                return true;
            }
            else if (candidateFreq <= 5)
            {
                // Tha maximum frequence is 15 and havled to 7 after a reset to age the history.
                // An attack exploits that a hot candidate is reject in favor of a hot victim.
                // The threshold of a warm candidate reduces the number of random acceptances
                // to minimize the impact on the hit rate.
                return false;
            }

            int random = this.random.Value.Next();
            return ((random & 127) == 0);
        }

        /// <summary>
        /// Expires entries that have expired by access, write or variable
        /// </summary>
        void ExpireEntries()
        {
            long now = ExpirationTicker.Ticks();
            ExpireAfterAccessEntries(now);
            ExpireAfterWriteEntries(now);
            ExpireVariableEntries(now);
        }

        /// <summary>
        /// Expires entries in an access-order deque
        /// </summary>
        /// <param name="now"></param>
        void ExpireAfterAccessEntries(long now)
        {
            if (!ExpiresAfterAccess)
                return;

            ExpireAfterAccessEntries(AccessOrderEdenDeque, now);
            if (Evicts)
            {
                ExpireAfterAccessEntries(AccessOrderProbationDeque, now);
                ExpireAfterAccessEntries(AccessOrderProtectedDeque, now);
            }
        }

        /// <summary>
        /// Expires entries in an access-order deque
        /// </summary>
        /// <param name="accessOrderDeque"></param>
        /// <param name="now"></param>
        void ExpireAfterAccessEntries(AccessOrderDeque<Node<K, V>> accessOrderDeque, long now)
        {
            long duration = ExpiresAfterAccessNanos;
            for (; ; )
            {
                Node<K, V> node = accessOrderDeque.PeekFirst();
                if ((node == null) || ((now - node.AccessTime) < duration))
                    return;

                EvictEntry(node, RemovalCause.EXPIRED, now);
            }
        }

        /// <summary>
        /// Expires entries on the write-order deque
        /// </summary>
        /// <param name="now"></param>
        void ExpireAfterWriteEntries(long now)
        {
            if (!ExpiresAfterWrite)
                return;

            long duration = ExpiresAfterWriteNanos;
            for (; ; )
            {
                Node<K, V> node = WriteOrderDeque.PeekFirst();
                if ((node == null) || ((now - node.WriteTime) < duration))
                    break;

                EvictEntry(node, RemovalCause.EXPIRED, now);
            }
        }

        /// <summary>
        /// Expires entries in the timer wheel.
        /// </summary>
        /// <param name="now"></param>
        void ExpireVariableEntries(long now)
        {
            if (ExpiresVariable)
                TimerWheel.Advance(now);
        }

        bool HasExpired(Node<K, V> node, long now)
        {
            bool accessExpired = ExpiresAfterAccess && ((now - node.AccessTime) >= ExpiresAfterAccessNanos);
            bool writeExpired = ExpiresAfterWrite && ((now - node.WriteTime) >= ExpiresAfterWriteNanos);
            bool varExpired = ExpiresVariable && ((now - node.VariableTime) >= 0);

            // using bitwise 'or' here due to the idea that they are considerably faster than logical operators.
            return accessExpired | writeExpired | varExpired;
        }

        /// <summary>
        /// Attempts to evict the entry based on the given removal cause. A removal due to expiration or
        /// size may be ignored if the entry was updated and is no longer eligibile for eviction.
        /// </summary>
        /// <param name="node">The entry to evict.</param>
        /// <param name="cause">The reason to evict.</param>
        /// <param name="now">the current time, used only if expiring</param>
        /// <returns>true if the entry was evicted.</returns>
        internal bool EvictEntry(Node<K, V> node, RemovalCause cause, long now)
        {
            K key = node.Key;
            V[] value = (V[])new V[1];
            bool[] removed = new bool[1];
            bool[] resurrect = new bool[1];
            RemovalCause[] actualCause = new RemovalCause[1];

            data.AddOrUpdate((K)node.KeyReference,
                (k) =>
                {
                    // TODO: Randy Lynn 2018-07-15... If the key doesn't exist at all.. what does it mean to just add it back into the cache??
                    return node;
                },
                (k, n) =>
                {
                    if (n != node)
                        return n;

                    lock (n)
                    {
                        value[0] = n.Value;
                        actualCause[0] = (key == null) || (value[0] == null) ? RemovalCause.COLLECTED : cause;

                        if (actualCause[0] == RemovalCause.EXPIRED)
                        {
                            bool expired = false;
                            if (ExpiresAfterAccess)
                                expired |= ((now - n.AccessTime) >= ExpiresAfterAccessNanos);

                            if (ExpiresAfterWrite)
                                expired |= ((now - n.WriteTime) >= ExpiresAfterWriteNanos);

                            if (ExpiresVariable)
                                expired |= ((now - n.VariableTime) <= now);

                            if (!expired)
                            {
                                resurrect[0] = true;
                                return n;
                            }
                        }
                        else if (actualCause[0] == RemovalCause.SIZE)
                        {
                            int weight = node.Weight;
                            if (weight == 0)
                            {
                                resurrect[0] = true;
                                return n;
                            }
                        }

                        writer.Delete(key, value[0], actualCause[0]);
                        MakeDead(n);
                    }

                    removed[0] = true;
                    return null;
                });

            if (resurrect[0])
                return false;

            // if the eviction fails due to a concurrent removal of the victim, that removal 
            // may cancel out the addition that triggered this eviction. the victim is eagerly
            // unlinked before the removal task so that if an eviction is still required
            // then a new victim will be chosen for removal.
            if (node.InEden && (Evicts || ExpiresAfterAccess))
            {
                AccessOrderEdenDeque.Remove(node);
            }
            else if (Evicts)
            {
                if (node.InMainProbation)
                {
                    AccessOrderProbationDeque.Remove(node);
                }
                else
                {
                    AccessOrderProtectedDeque.Remove(node);
                }
            }

            if (ExpiresAfterWrite)
            {
                WriteOrderDeque.Remove(node);
            }
            else if (ExpiresVariable)
            {
                TimerWheel.Deschedule(node);
            }

            if (removed[0])
            {
                StatsCounter.RecordEviction(node.Weight);
                if (HasRemovalListener)
                    // Notify the listener only if the entry was evicted. This must be performed
                    // as the last step during eviction to safe guard against the executor
                    // rejecting the notification task.
                    NotifyRemoval(key, value[0], actualCause[0]);
            }
            else
            {
                // Eagerly decrement the size to potentially avoid an additional eviction, rather
                // than wait for the removal task to do it on the next maintenance cycle.
                MakeDead(node);
            }

            return true;
        }

        /// <summary>
        /// Performs the post-processing work required after a read.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="now"></param>
        /// <param name="recordHits"></param>
        internal void AfterRead(Node<K, V> node, long now, bool recordHits)
        {
            if (recordHits)
                StatsCounter.RecordHits(1);

            bool delayable = SkipReadBuffer || (readBuffer.Offer(node) != OfferStatusCodes.FULL);

            if (ShouldDrainBuffers(delayable))
                ScheduleDrainBuffers();

            RefreshIfNeeded(node, now);
        }

        /// <summary>
        /// Returns if the cache should bypass the read buffer.
        /// </summary>
        bool SkipReadBuffer
        {
            get { return FastPath && FrequencySketch.IsNotInitialized; }
        }

        /// <summary>
        /// Asynchronously refreshes the entry if eligible.
        /// </summary>
        /// <param name="node">The entry in the cache to refresh</param>
        /// <param name="now">The current time, in nanoseconds</param>
        void RefreshIfNeeded(Node<K, V> node, long now)
        {
            if (!RefreshAfterWrite)
                return;

            K key;
            V oldValue;

            long oldWriteTime = node.WriteTime;
            long refreshWriteTime = (now + AsyncExpiry<K, V>.ASYNC_EXPIRY);

            if (((now - oldWriteTime) > RefreshAfterWriteNanos)
                && (!EqualityComparer<K>.Default.Equals((key = node.Key), default(K)) && !EqualityComparer<V>.Default.Equals((oldValue = node.Value), default(V)))
                && node.CasWriteTime(oldWriteTime, refreshWriteTime))
            {
                try
                {
                    Task<V> refresh = null;

                    if (isAsync)
                    {

                    }
                    else
                    {
                        refresh = cacheLoader.ReloadAsync(key, oldValue);
                    }

                    refresh.ContinueWith((t) =>
                    {
                        long loadTime = StatsTicker.Ticks() - now;
                        if (t.IsFaulted)
                        {
                            node.CasWriteTime(refreshWriteTime, oldWriteTime);
                            StatsCounter.RecordLoadFailure(loadTime);
                            return;
                        }

                        V newValue = t.Result;

                        V value = (isAsync && (t.Result != null)) ? refresh.Result : newValue;

                        bool[] discard = new bool[1];
                        Compute(key, (k, currentValue) =>
                        {
                            if (currentValue == null)
                                return value;
                            else if ((EqualityComparer<V>.Default.Equals(currentValue, oldValue) && (node.WriteTime == refreshWriteTime)))
                                return value;
                            discard[0] = true;
                            return currentValue;
                        }, false, false);


                        if (discard[0] && HasRemovalListener)
                            NotifyRemoval(key, value, RemovalCause.REPLACED);

                        if (EqualityComparer<V>.Default.Equals(newValue, default(V)))
                        {
                            StatsCounter.RecordLoadFailure(loadTime);
                        }
                        else
                        {
                            StatsCounter.RecordLoadSuccess(loadTime);
                        }
                    });
                }
                catch (Exception e)
                {
                    node.CasWriteTime(refreshWriteTime, oldWriteTime);
                    // TODO: logging..
                }
            }
        }

        /// <summary>
        /// Returns the expiration time for the entry after being created.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expiry"></param>
        /// <param name="now"></param>
        /// <returns></returns>
        long ExpireAfterCreate(K key, V value, IExpiry<K, V> expiry, long now)
        {
            if (ExpiresVariable && (key != null) && (value != null))
            {
                long duration = expiry.ExpireAfterCreate(key, value, now);
                return isAsync ? (now + duration) : (now + Math.Min(duration, MAXIMUM_EXPIRY));
            }

            return 0L;
        }

        /// <summary>
        /// Returns the expiration time for the entry after being updated.
        /// </summary>
        /// <param name="node">The entry in the page replacement policy.</param>
        /// <param name="key">The key of the entry that was created.</param>
        /// <param name="value">The value of the entry that was updated.</param>
        /// <param name="expiry">The calculator for the expiration time.</param>
        /// <param name="now">The current time, in nanoseconds.</param>
        /// <returns>The expiration time.</returns>
        long ExpireAfterUpdate(Node<K, V> node, K key, V value, IExpiry<K, V> expiry, long now)
        {
            if (ExpiresVariable && (key != null) && (value != null))
            {
                long currentDuration = Math.Max(1, node.VariableTime - now);
                long duration = expiry.ExpireAfterUpdate(key, value, now, currentDuration);
                return isAsync ? (now + duration) : (now + Math.Min(duration, MAXIMUM_EXPIRY));
            }

            return 0L;
        }

        /// <summary>
        /// Returns the access time for the entry after a read.
        /// </summary>
        /// <param name="node">The entry in the page replacement policy.</param>
        /// <param name="key">The key of the entry that was read.</param>
        /// <param name="value">The value of the entry that was read.</param>
        /// <param name="expiry">The calculator for the expiration time.</param>
        /// <param name="now">the current time in nanoseconds</param>
        /// <returns>The expiration time</returns>
        long ExpireAfterRead(Node<K, V> node, K key, V value, IExpiry<K, V> expiry, long now)
        {
            if (ExpiresVariable && (key != null) && (value != null))
            {
                long currentDuration = Math.Max(1, node.VariableTime - now);
                long duration = expiry.ExpireAfterRead(key, value, now, currentDuration);
                return isAsync ? (now + duration) : (now + Math.Min(duration, MAXIMUM_EXPIRY));
            }

            return 0L;
        }

        internal void SetVariableTime(Node<K, V> node, long expirationTime)
        {
            if (ExpiresVariable)
                node.VariableTime = expirationTime;
        }

        internal void SetWriteTime(Node<K, V> node, long now)
        {
            if (ExpiresAfterWrite || RefreshAfterWrite)
                node.WriteTime = now;
        }

        internal void SetAccessTime(Node<K, V> node, long now)
        {
            if (ExpiresAfterAccess)
                node.AccessTime = now;
        }

        /// <summary>
        /// Performs the post-processing work required after a write.
        /// </summary>
        /// <param name="task"></param>
        void AfterWrite(Task task)
        {
            if (BuffersWrites)
            {
                for (int i = 0; i < WRITE_BUFFER_RETRIES; i++)
                {
                    if (WriteBuffer.Enqueue(task))
                    {
                        ScheduleAfterWrite();
                        return;
                    }

                    ScheduleDrainBuffers();
                }

                // TODO: The original java code in this function does some work to force a task to run.. I think we should leave these lines out.
            }
            else
            {
                ScheduleAfterWrite();
            }
        }

        /// <summary>
        /// Conditionally schedules the asynchronous maintenance task after a write operation. If the task
        /// status was IDEL or REQUIRED then the maintenance task is scheduled immediately. If it is already
        /// processing then it is set to transition to REQUIRED upon completion so that a new execution is 
        /// triggered by the next operation.
        /// </summary>
        internal void ScheduleAfterWrite()
        {
            for (; ; )
            {
                switch (DrainStatus)
                {
                    case IDLE:
                        DrainStatus = REQUIRED;
                        ScheduleDrainBuffers();
                        return;
                    case REQUIRED:
                        ScheduleDrainBuffers();
                        return;
                    case PROCESSING_TO_IDLE:
                        if (CasDrainStatus(PROCESSING_TO_IDLE, PROCESSING_TO_REQUIRED))
                            return;
                        continue;
                    case PROCESSING_TO_REQUIRED:
                        return;
                    default:
                        throw new InvalidOperationException();

                }
            }
        }

        /// <summary>
        /// Attempts to schedule an asynchronous task to apply the pending operations to the page
        /// replacement policy.
        /// </summary>
        private void ScheduleDrainBuffers()
        {
            if (DrainStatus >= PROCESSING_TO_IDLE)
                return;

            lock (evictionLock)
            {
                try
                {
                    int drainStatus = DrainStatus;

                    if (drainStatus >= PROCESSING_TO_IDLE)
                        return;

                    DrainStatus = PROCESSING_TO_IDLE;
                    Task.Run(() => { PerformCleanup(); });
                }
                catch (Exception e)
                {
                    Maintenance();
                }
            }
        }
        #endregion

        /// <summary>
        /// Evicts entries from the eden space into the main space while the eden size exceeds a maximum.
        /// </summary>
        /// <returns>The number of candidate entries evicted from the eden space</returns>
        internal int EvictFromEden()
        {
            int candidates = 0;
            Node<K, V> node = AccessOrderEdenDeque.Peek();
            while (EdenWeightedSize > EdenMaximum)
            {
                if (node == null)
                    break;

                Node<K, V> next = node.GetNextInAccessOrder();
                if (node.Weight != 0)
                {
                    node.MakeMainProbation();
                    AccessOrderEdenDeque.Remove(node);
                    AccessOrderProbationDeque.Add(node);
                    candidates++;

                    // TODO: convert policy weight to ulong??
                    EdenWeightedSize = EdenWeightedSize - (ulong)node.PolicyWeight;
                }

                node = next;
            }

            return candidates;
        }

        public override void CleanUp()
        {
            try
            {
                PerformCleanup();
            }
            catch (Exception e)
            {
                // TODO: logging
            }
        }

        private void PerformCleanup()
        {
            lock (evictionLock)
            {
                Maintenance();
            }
        }

        /// <summary>
        /// Performs the pending maintenance work and sets the state flags during processing to avoid
        /// excess scheduling attempts. The read buffer, write buffer and refernece queues are drained,
        /// followed by expiration, and size-based eviction.
        /// </summary>
        internal void Maintenance()
        {
            lock (evictionLock)
            {
                DrainStatus = PROCESSING_TO_IDLE;
                try
                {
                    DrainReadBuffer();
                    DrainWriteBuffer();

                    DrainKeyReferences();
                    DrainValueReferences();

                    ExpireEntries();
                    EvictEntries();
                }
                finally
                {
                    if ((DrainStatus != PROCESSING_TO_IDLE) || !CasDrainStatus(PROCESSING_TO_IDLE, IDLE))
                        DrainStatus = REQUIRED;
                }
            }
        }

        /// <summary>
        /// Drains the weak key references queue.
        /// </summary>
        private void DrainKeyReferences()
        {
            if (!CollectKeys)
                return;

            // TODO: Handle weak reference collection.. Java has a concept of a referencequeue.. and when a weakreference is collected, the weak reference is added to the queue.
            // .NET does not have this concept.. essentially what would have to happen is we would have to iterate over *ALL* the keys checking to see if the key has been 
            // collected.. if it had, then remove.. 
            //while ((keyRef = KeyReferenceQueue().Dequeue()) != null)
            //{
            //    Node<K, V> node = null;
            //    data.TryGetValue((K)keyRef, out node);

            //    if (node != null)
            //        EvictEntry(node, RemovalCause.COLLECTED, 0L);
            //}
        }

        /// <summary>
        /// Drains the weak value references queue.
        /// </summary>
        private void DrainValueReferences()
        {
            if (!CollectValues)
                return;

            // TODO: see same problem above with DrainKeyreferences.
            //while ((valueRef = ValueReferenceQueue().Dequeue()) != null)
            //{
            //    Node<K, V> node = null;
            //    data.TryGetValue(valueRef.KeyReference, out node);

            //    if ((node != null) && (valueRef == node.ValueReference))
            //        EvictEntry(node, RemovalCause.COLLECTED, 0L);
            //}
        }

        /// <summary>
        /// Drains the read buffer.
        /// </summary>
        private void DrainReadBuffer()
        {
            if (!SkipReadBuffer)
                readBuffer.DrainTo(accessPolicy);
        }

        /// <summary>
        /// Updates the node's location in the page replacement policy.
        /// </summary>
        /// <param name="node"></param>
        private void OnAccess(Node<K, V> node)
        {
            if (Evicts)
            {
                K key = node.Key;

                if (EqualityComparer<K>.Default.Equals(key, default(K)))
                    return;

                FrequencySketch.Increment(key);
                if (node.InEden)
                {
                    Reorder(AccessOrderEdenDeque, node);
                }
                else if (node.InMainProbation)
                {
                    ReorderProbation(node);
                }
                else
                {
                    Reorder(AccessOrderProtectedDeque, node);
                }
            }
            else if (ExpiresAfterAccess)
            {
                Reorder(AccessOrderEdenDeque, node);
            }

            if (ExpiresVariable)
                TimerWheel.Reschedule(node);
        }

        /// <summary>
        /// Promote the <paramref name="node"/> from probation to protected on an access.
        /// </summary>
        /// <param name="node"></param>
        private void ReorderProbation(Node<K, V> node)
        {
            if (!AccessOrderProbationDeque.Contains(node))
            {
                // ignore stale accesses for an entry that is no longer present.
                return;
            }
            else if ((ulong)node.PolicyWeight > MainProtectedMaximum)
            {
                return;
            }

            ulong mainProtectedWeightedSize = MainProtectedWeightedSize + (ulong)node.PolicyWeight;
            AccessOrderProbationDeque.Remove(node);
            AccessOrderProtectedDeque.Add(node);
            node.MakeMainProtected();

            ulong mainProtectedMaximum = MainProtectedMaximum;
            while (MainProtectedWeightedSize > mainProtectedMaximum)
            {
                Node<K, V> demoted = AccessOrderProtectedDeque.First;
                if (demoted == null)
                    break;

                demoted.MakeMainProbation();
                AccessOrderProbationDeque.Add(demoted);
                mainProtectedWeightedSize -= (ulong)demoted.PolicyWeight;
            }

            MainProtectedWeightedSize = mainProtectedWeightedSize;
        }

        /// <summary>
        /// Updates the node's location in the policy's deque.
        /// </summary>
        /// <param name="deque"></param>
        /// <param name="node"></param>
        void Reorder(ILinkedDeque<Node<K, V>> deque, Node<K, V> node)
        {
            if (deque.Contains(node))
                deque.MoveToBack(node);
        }

        /// <summary>
        /// Drains the write buffer.
        /// </summary>
        void DrainWriteBuffer()
        {
            if (!BuffersWrites)
                return;

            for (int i = 0; i < WRITE_BUFFER_MAX; i++)
            {
                Task task = WriteBuffer.Dequeue();
                if (task == null)
                    break;

                task.Start();
            }
        }

        private void MakeDead(Node<K, V> node)
        {
            lock (node)
            {
                if (node.IsDead)
                    return;

                if (Evicts)
                {
                    if (node.InEden)
                    {
                        EdenWeightedSize = EdenWeightedSize - (ulong)node.Weight;
                    }
                    else if (node.InMainProtected)
                    {
                        MainProtectedWeightedSize = MainProtectedWeightedSize - (ulong)node.Weight;
                    }

                    WeightedSize = WeightedSize - (ulong)node.Weight;
                }

                node.Die();
            }
        }

        #region ConcurrentDictionary implementation

        public override bool IsEmpty
        {
            get { return data.IsEmpty; }
        }

        public override int Count
        {
            get { return data.Count; }
        }

        public override void Clear()
        {
            lock (evictionLock)
            {
                long now = ExpirationTicker.Ticks();

                Task runner;
                while (BuffersWrites && (runner = WriteBuffer.Dequeue()) != null)
                {
                    runner.Start();
                }

                foreach (Node<K, V> node in data.Values)
                {
                    RemoveNode(node, now);
                }

                readBuffer.DrainTo((e) => { });
            }
        }

        public override bool ContainsKey(K key)
        {
            Node<K, V> node = null;
            K lookupKey = nodeFactory.NewLookupKey(key);

            if (data.TryGetValue(lookupKey, out node))
            {
                if (!EqualityComparer<V>.Default.Equals(node.Value, default(V)) && !HasExpired(node, ExpirationTicker.Ticks()))
                    return true;
            }

            return false;
        }

        public override bool TryGetValue(K key, out V value)
        {
            long tmpVar = 0L;
            V transient = TryGetValueQuietly(key, ref tmpVar);

            value = transient;
            if (!EqualityComparer<V>.Default.Equals(transient, default(V)))
                return true;
            else
                return false;
        }

        public override V TryGetValue(K key, bool recordStats)
        {

            Node<K, V> node = null;
            data.TryGetValue(nodeFactory.NewLookupKey(key), out node);

            if (node == null)
            {
                if (recordStats)
                    StatsCounter.RecordMisses(1);

                return default(V);
            }

            long now = ExpirationTicker.Ticks();
            if (HasExpired(node, now))
            {
                if (recordStats)
                {
                    StatsCounter.RecordMisses(1);
                }
                ScheduleDrainBuffers();
                return default(V);
            }

            V value = node.Value;

            if (!IsComputingAsync(node))
            {
                SetVariableTime(node, ExpireAfterRead(node, node.Key, value, Expiry, now));
                SetAccessTime(node, now);
            }

            AfterRead(node, now, recordStats);
            return value;
        }

        public override V TryGetValueQuietly(K key, ref long writeTime)
        {
            V value;
            Node<K, V> node = null;
            data.TryGetValue(nodeFactory.NewLookupKey(key), out node);

            if ((node == null) || ((value = node.Value) == null) || HasExpired(node, ExpirationTicker.Ticks()))
            {
                return default(V);
            }

            writeTime = node.WriteTime;
            return value;
        }

        /// <summary>
        /// Removes a <paramref name="key"/> from the cache.
        /// </summary>
        /// <param name="key">the object to remove with the matching key</param>
        /// <returns>True if the item existed and was removed, false if it did not exist</returns>
        public override bool TryRemove(K key, out V value)
        {
            value = default(V);
            bool rval = false;
            if (HasWriter)
                rval = RemoveWithWriter(key, out value);
            else
                rval = RemoveNoWriter(key, out value);

            return rval;
        }

        public override Dictionary<K, V> TryGetAll(IEnumerable<K> keys)
        {
            HashSet<K> uniqueKeys = new HashSet<K>();
            foreach (K obj in keys)
            {
                uniqueKeys.Add(obj);
            }

            int misses = 0;
            long now = ExpirationTicker.Ticks();
            Dictionary<K, V> result = new Dictionary<K, V>(uniqueKeys.Count);
            foreach (K key in uniqueKeys)
            {
                V value;
                Node<K, V> node;
                data.TryGetValue(nodeFactory.NewLookupKey(key), out node);

                if ((node == null) || ((value = node.Value) == null) || HasExpired(node, now))
                {
                    misses++;
                }
                else
                {
                    result.Add(key, value);

                    if (!IsComputingAsync(node))
                    {
                        SetVariableTime(node, ExpireAfterRead(node, key, value, Expiry, now));
                        SetAccessTime(node, now);
                    }

                    AfterRead(node, now, false);
                }
            }

            StatsCounter.RecordMisses(misses);
            StatsCounter.RecordHits(result.Count);
            return result;
        }
        #endregion


        /// <summary>
        /// Adds the node to the page replacement policy.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="weight"></param>
        // TODO: convert int weight to ulong?
        private void AddNodeAsync(Node<K, V> node, int weight)
        {
            if (Evicts)
            {
                node.PolicyWeight = weight;
                ulong weightedSize = WeightedSize;
                WeightedSize = weightedSize + (ulong)weight;
                EdenWeightedSize = EdenWeightedSize + (ulong)weight;

                ulong maximum = Maximum;
                if (weightedSize >= (maximum >> 1))
                {
                    ulong capacity = IsWeighted ? (ulong)data.Count : maximum;
                    FrequencySketch.EnsureCapacity(capacity);
                }

                K key = node.Key;
                if (EqualityComparer<K>.Default.Equals(key, default(K)))
                    FrequencySketch.Increment(key);
            }

            bool isAlive;
            lock (node)
            {
                isAlive = node.IsAlive;
            }

            if (isAlive)
            {
                if (ExpiresAfterWrite)
                {
                    WriteOrderDeque.Add(node);
                }

                if (Evicts || ExpiresAfterAccess)
                {
                    AccessOrderEdenDeque.Add(node);
                }

                if (ExpiresVariable)
                {
                    TimerWheel.Schedule(node);
                }
            }

            if (IsComputingAsync(node))
            {
                lock (node)
                {
                    // TODO: Check for async if the node has it's value computed already..
                    //if (!Async.IsReady(node.Value))
                    //{
                    long expirationTime = ExpirationTicker.Ticks() + AsyncExpiry<K, V>.ASYNC_EXPIRY;
                    SetVariableTime(node, expirationTime);
                    SetAccessTime(node, expirationTime);
                    SetWriteTime(node, expirationTime);
                    //}
                }
            }
        }

        /// <summary>
        /// Removes a node from the page replacement policy.
        /// </summary>
        /// <param name="node"></param>
        private void RemoveNodeAsync(Node<K, V> node)
        {
            if (node.InEden && (Evicts || ExpiresAfterAccess))
            {
                AccessOrderEdenDeque.Remove(node);
            }
            else if (Evicts)
            {
                if (node.InMainProbation)
                {
                    AccessOrderProbationDeque.Remove(node);
                }
                else
                {
                    AccessOrderProtectedDeque.Remove(node);
                }
            }

            if (ExpiresAfterWrite)
            {
                WriteOrderDeque.Remove(node);
            }
            else if (ExpiresVariable)
            {
                TimerWheel.Deschedule(node);
            }

            MakeDead(node);
        }

        /// <summary>
        /// Updates the weighted size.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="weightDifference"></param>
        // TODO: consider updating weightDifference to be a ulong?
        private void UpdateWeightAsync(Node<K, V> node, int weightDifference)
        {
            if (Evicts)
            {
                if (node.InEden)
                {
                    EdenWeightedSize = EdenWeightedSize + (ulong)weightDifference;
                }
                else if (node.InMainProtected)
                {
                    MainProtectedWeightedSize = MainProtectedMaximum + (ulong)weightDifference;
                }
                WeightedSize = WeightedSize + (ulong)weightDifference;
                node.PolicyWeight = node.PolicyWeight + weightDifference;

                if (Evicts || ExpiresAfterAccess)
                {
                    OnAccess(node);
                }

                if (ExpiresAfterWrite)
                {
                    Reorder(WriteOrderDeque, node);
                }
                else if (ExpiresVariable)
                {
                    TimerWheel.Reschedule(node);
                }
            }
        }

        // TODO: remove in favor of "count"?
        public override long EstimatedSize()
        {
            return data.Count;
        }

        private void RemoveNode(Node<K, V> node, long now)
        {
            K key = node.Key;
            V value = default(V);

            RemovalCause cause = RemovalCause.UNKNOWN;

            Node<K, V> removedNode = null;
            bool wasRemoved = false;
            data.TryRemove((K)node.KeyReference, out removedNode);
            if (removedNode != node)
            {
                data.GetOrAdd(node.Key, node);
            }
            else
            {
                wasRemoved = true;
                lock (removedNode)
                {
                    if (EqualityComparer<K>.Default.Equals(key, default(K)) || EqualityComparer<V>.Default.Equals(removedNode.Value, default(V)))
                    {
                        cause = RemovalCause.COLLECTED;
                    }
                    else if (HasExpired(node, now))
                    {
                        cause = RemovalCause.EXPIRED;
                    }
                    else
                    {
                        cause = RemovalCause.EXPLICIT;
                    }

                    writer.Delete(key, removedNode.Value, cause);
                    MakeDead(removedNode);
                }
            }

            if (node.InEden && (Evicts || ExpiresAfterAccess))
            {
                AccessOrderEdenDeque.Remove(node);
            }
            else if (Evicts)
            {
                if (node.InMainProbation)
                {
                    AccessOrderProbationDeque.Remove(node);
                }
                else
                {
                    AccessOrderProtectedDeque.Remove(node);
                }
            }

            if (ExpiresAfterWrite)
            {
                WriteOrderDeque.Remove(node);
            }
            else if (ExpiresVariable)
            {
                TimerWheel.Deschedule(node);
            }

            if (wasRemoved && HasRemovalListener)
                NotifyRemoval(key, removedNode.Value, cause);
        }


        public override V Compute(K key, Func<K, V, V> remappingFunction, bool recordMiss, bool recordLoad)
        {
            if (EqualityComparer<K>.Default.Equals(key, default(K)))
                throw new ArgumentNullException("key", "key is a required parameter");

            long now = ExpirationTicker.Ticks();


            Func<K, V, V> statsAwareRemapping = StatsAware(remappingFunction, recordMiss, recordLoad);

            return Remap(key, null, statsAwareRemapping, now, true);
        }

        /// <summary>
        /// Attempts to compute a mapping for the specified key and its current mapped alue (
        /// or <see langword="null"/> if there is no current mapping.
        /// 
        /// <para>An entry that has expired or been reference collected is evicted and the computation
        /// continues as if the entry had not been present. This method does not pre-screen and does not
        /// wrap the remapping function to be statistics aware.</para>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyRef"></param>
        /// <param name="remappingFunction"></param>
        /// <param name="now"></param>
        /// <param name="computeIfAbsent"></param>
        /// <returns></returns>
        internal V Remap(K key, object keyRef, Func<K, V, V> remappingFunction, long now, bool computeIfAbsent)
        {
            K nodeKey = default(K);
            V oldValue = default(V);
            V newValue = default(V);
            Node<K, V> removed;

            int[] weight = new int[2];
            RemovalCause cause = RemovalCause.UNKNOWN;



            if (cause != RemovalCause.UNKNOWN)
            {
                if (WasEvicted(cause))
                {
                    StatsCounter.RecordEviction(weight[9]);
                }
                else
                {
                    NotifyRemoval(nodeKey, oldValue, cause);
                }
            }

            // new C# feature to have local functions inside of functions..
            bool WasEvicted(RemovalCause aCause)
            {
                return (aCause == RemovalCause.EXPIRED || aCause == RemovalCause.COLLECTED || aCause == RemovalCause.SIZE);
            }

            return newValue;
        }


        internal SortedDictionary<K, V> EvictionOrder(uint limit, Func<V, V> transformer, bool hottest)
        {
            throw new NotImplementedException();
        }

        internal IOrderedDictionary ExpireAfterAccessOrder(uint limit, Func<V, V> transformer, bool oldest)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns an unmodifiable <see cref="IReadOnlyDictionary{TKey, TValue}"/> ordered in write expiration order,
        /// either ascending or descending. Beware that obtaining the mappings is NOT a constant-time operation.
        /// </summary>
        /// <param name="limit">the maximum number of entries</param>
        /// <param name="transformer">a function that unwraps the value</param>
        /// <param name="oldest">the iteration order</param>
        /// <returns></returns>
        internal IOrderedDictionary ExpireAfterWriteOrder(uint limit, Func<V, V> transformer, bool oldest)
        {
            IEnumerator<Node<K, V>> enumerator = oldest ? (IEnumerator<Node<K, V>>)WriteOrderDeque.GetEnumerator() : (IEnumerator<Node<K, V>>)WriteOrderDeque.GetDescendingEnumerator();
            return FixedSnapshot(enumerator, limit, transformer);
        }

        /// <summary>
        /// Returns an unmodifiable <see cref="IReadOnlyDictionary{TKey, TValue}"/> ordered by the provided <paramref name="enumerator"/>. Beware that
        /// obtaining the mappings it NOT a constant-time operation.
        /// </summary>
        /// <param name="enumerator"></param>
        /// <param name="limit">the maximum number of entries</param>
        /// <param name="transformer">a function that unwraps the value</param>
        /// <returns>An <see cref="IReadOnlyDictionary{TKey, TValue}"/> in the desired order.</returns>
        internal IOrderedDictionary FixedSnapshot(IEnumerator<Node<K, V>> enumerator, uint limit, Func<V, V> transformer)
        {
            Contract.Requires(limit >= 0, "limit must be a positive number");

            lock (evictionLock)
            {
                Maintenance();

                uint initialCapacity = Math.Min(limit, (uint)Count);

                // TODO: This sucks!! We need an OrderedDictionary generic. This will work temporarily.
                OrderedDictionary result = new OrderedDictionary((int)initialCapacity);

                while (enumerator.MoveNext())
                {
                    if (result.Count >= limit)
                        break;

                    Node<K, V> node = enumerator.Current;

                    K key = node.Key;
                    V value = transformer(node.Value);

                    if (!EqualityComparer<K>.Default.Equals(key, default(K)) && !EqualityComparer<V>.Default.Equals(value, default(V)) && node.IsAlive)
                        result.Add(key, value);
                }

                return result;
            }
        }

        internal IOrderedDictionary VariableSnapshot(bool ascending, uint limit, Func<V, V> transformer)
        {
            lock (evictionLock)
            {
                Maintenance();

                return TimerWheel.Snapshot(ascending, (int)limit, transformer);
            }
        }

        public override V GetOrAdd(K key, Func<K, V, V> mappingFunction, bool recordStats, bool recordLoad)
        {
            throw new NotImplementedException();
        }

        public override V Add(K key, V value, bool notifyWriter)
        {
            throw new NotImplementedException();
        }

        // TODO: rethink inheriting from ConcurrentDictionary??
        public override bool TryAdd(K key, V value)
        {
            return TryAdd(key, value, Expiry, true);
        }

        /// <summary>
        /// Adds a node to the policy and the data store. If an existing node is found, then its value is
        /// updated if allowed.
        /// </summary>
        /// <param name="key">Key with which the specified value is to be associated</param>
        /// <param name="value">Value to be associated with the specified key.</param>
        /// <param name="expiry">The calculator for expiration time.</param>
        /// <param name="notifyWriter">if the writer should be notified for an inserted or updated entry.</param>
        /// <returns></returns>
        public bool TryAdd(K key, V value, IExpiry<K, V> expiry, bool notifyWriter)
        {
            if (EqualityComparer<K>.Default.Equals(key, default(K)))
                throw new ArgumentNullException("key", "key cannot be null.");

            if (EqualityComparer<V>.Default.Equals(value, default(V)))
                throw new ArgumentNullException("value", "value cannot be null.");

            Node<K, V> node = null;
            long now = ExpirationTicker.Ticks();
            int newWeight = weigher.Weigh(key, value);

            for (; ; )
            {
                Node<K, V> prior = null;
                K lookupKey = nodeFactory.NewLookupKey(key);
                data.TryGetValue(lookupKey, out prior);
                if (prior == null)
                {
                    if (node == null)
                    {
                        node = nodeFactory.NewNode(key, value, newWeight, now);
                        SetVariableTime(node, ExpireAfterCreate(key, value, expiry, now));
                    }

                    if (notifyWriter && HasWriter)
                    {
                        Node<K, V> computed = node;
                        if (!data.TryGetValue(lookupKey, out prior))
                        {
                            data.TryAdd((K)node.KeyReference, computed);
                        }

                        if (prior == node)
                        {
                            AfterWrite(new Task(() => AddTask(node, newWeight)));
                            return true;
                        }
                    }
                    else
                    {
                        if (data.TryAdd((K)node.KeyReference, node))
                        {
                            AfterWrite(new Task(() => AddTask(node, newWeight)));
                            return true;
                        }
                    }
                }

                V oldValue;
                long varTime;
                int oldWeight;
                bool expired = false;
                bool mayUpdate = true;
                bool withinTolerance = true;
                lock (prior)
                {
                    if (!prior.IsAlive)
                        continue;

                    oldValue = prior.Value;
                    oldWeight = prior.Weight;
                    if (EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                    {
                        varTime = ExpireAfterCreate(key, value, expiry, now);
                        writer.Delete(key, default(V), RemovalCause.COLLECTED);
                    }
                    else if (HasExpired(prior, now))
                    {
                        expired = true;
                        varTime = ExpireAfterCreate(key, value, expiry, now);
                        writer.Delete(key, oldValue, RemovalCause.EXPIRED);
                    }
                    // TODO: implement this..
                    //else if (onlyIfAbsent)
                    //{
                    //    mayUpdate = false;
                    //    varTime = ExpireAfterRead(prior, key, value, expiry, now);
                    //}
                    else
                        varTime = ExpireAfterUpdate(prior, key, value, expiry, now);

                    if (notifyWriter && (expired || (mayUpdate && (!EqualityComparer<V>.Default.Equals(value, oldValue)))))
                    {
                        writer.Write(key, value);
                    }

                    if (mayUpdate)
                    {
                        withinTolerance = ((now - prior.WriteTime) > EXPIRE_WRITE_TOLERANCE);
                        SetWriteTime(prior, now);

                        prior.Weight = (int)newWeight;
                        prior.Value = value;
                    }

                    SetVariableTime(prior, varTime);
                    SetAccessTime(prior, now);
                }

                if (HasRemovalListener)
                {
                    if (expired)
                    {
                        NotifyRemoval(key, oldValue, RemovalCause.EXPIRED);
                    }
                    else if (EqualityComparer<V>.Default.Equals(oldValue, default(V)))
                    {
                        NotifyRemoval(key, default(V), RemovalCause.COLLECTED);
                    }
                    else if (mayUpdate && (!EqualityComparer<V>.Default.Equals(value, oldValue)))
                    {
                        NotifyRemoval(key, oldValue, RemovalCause.REPLACED);
                    }
                }

                int weightedDifference = mayUpdate ? (newWeight - oldWeight) : 0;
                if (EqualityComparer<V>.Default.Equals(oldValue, default(V)) || (weightedDifference != 0) || expired)
                {
                    AfterWrite(UpdateTask(prior, weightedDifference));
                }
                // TODO: implement this..
                //else if (!onlyIfAbsent && ExpiresAfterWrite && withinTolerance)
                //{
                //    AfterWrite(UpdateTask(prior, weightedDifference));
                //}
                else
                {
                    if (mayUpdate)
                    {
                        SetWriteTime(prior, now);
                    }
                    AfterRead(prior, now, false);
                }

                // TODO: verify this entire method.. 
                return true;
            }
        }

        /// <summary>
        /// Removes a <paramref name="key"/> from the cache.
        /// </summary>
        /// <param name="key">the object to remove with the matching key</param>
        /// <returns>True if the item existed and was removed, false if it did not exist</returns>
        public override bool Remove(K key)
        {
            V oldVal = default(V);
            return TryRemove(key, out oldVal);
        }



        /// <summary>
        /// Removes the mapping for a key without notifying the writer.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal bool RemoveNoWriter(K key, out V oldValue)
        {
            oldValue = default(V);
            Node<K, V> node = null;
            if (!data.TryRemove(key, out node))
                return false;

            lock (node)
            {
                oldValue = node.Value;
                if (node.IsAlive)
                    node.Retire();
            }

            RemovalCause cause = RemovalCause.UNKNOWN;
            if (EqualityComparer<V>.Default.Equals(oldValue, default(V)))
            {
                cause = RemovalCause.COLLECTED;
            }
            else if (HasExpired(node, ExpirationTicker.Ticks()))
            {
                cause = RemovalCause.EXPIRED;
            }
            else
                cause = RemovalCause.EXPLICIT;

            if (HasRemovalListener)
                NotifyRemoval(key, oldValue, cause);

            AfterWrite(new Task(() => RemoveTask(node)));

            return true;
        }

        /// <summary>
        /// Removes the mapping for a <paramref name="key"/> after notifying the writer.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal bool RemoveWithWriter(K key, out V oldValue)
        {
            oldValue = default(V);
            Node<K, V> node = null;
            
            if (!data.TryRemove(key, out node))
                return false;

            lock (node)
            {
                oldValue = node.Value;
                if (node.IsAlive)
                    node.Retire();
            }

            RemovalCause cause = RemovalCause.UNKNOWN;
            if (EqualityComparer<V>.Default.Equals(oldValue, default(V)))
            {
                cause = RemovalCause.COLLECTED;
            }
            else if (HasExpired(node, ExpirationTicker.Ticks()))
            {
                cause = RemovalCause.EXPIRED;
            }
            else
                cause = RemovalCause.EXPLICIT;

            // TODO: Refactor this function with RemoveNoWriter.. they are the same with the addition of this writer.Delete statement.
            writer.Delete(key, oldValue, cause);

            AfterWrite(new Task(() => RemoveTask(node)));
            if (HasRemovalListener)
                NotifyRemoval(key, oldValue, cause);

            return true;
        }

        internal async Task UpdateTask(Node<K, V> node, int weightDifference)
        {
            if (Evicts)
            {
                if (node.InEden)
                {
                    EdenWeightedSize = EdenWeightedSize + (ulong)weightDifference;
                }
                else if (node.InMainProtected)
                {
                    MainProtectedWeightedSize = MainProtectedMaximum + (ulong)weightDifference;
                }
                WeightedSize = WeightedSize + (ulong)weightDifference;
                node.PolicyWeight = node.PolicyWeight + weightDifference;
            }

            if (Evicts || ExpiresAfterAccess)
            {
                OnAccess(node);
            }

            if (ExpiresAfterWrite)
            {
                Reorder(WriteOrderDeque, node);
            }
            else if (ExpiresVariable)
            {
                TimerWheel.Reschedule(node);
            }
        }

        internal async Task AddTask(Node<K, V> node, int weight)
        {
            if (Evicts)
            {
                node.PolicyWeight = weight;
                // TODO: change this back to just a signed variable??
                ulong weightedSize = WeightedSize;
                WeightedSize = weightedSize + (ulong)weight;
                EdenWeightedSize = EdenWeightedSize + (ulong)weight;

                ulong maximum = Maximum;
                if (weightedSize >= (maximum >> 1))
                {
                    ulong capacity = IsWeighted ? (ulong)data.Count : maximum;
                    FrequencySketch.EnsureCapacity(capacity);
                }

                K key = node.Key;
                if (!EqualityComparer<K>.Default.Equals(key, default(K)))
                    FrequencySketch.Increment(key);
            }

            bool isAlive;
            lock (node)
            {
                isAlive = node.IsAlive;
            }

            if (isAlive)
            {
                if (ExpiresAfterWrite)
                    WriteOrderDeque.Add(node);

                if (Evicts || ExpiresAfterAccess)
                    AccessOrderEdenDeque.Add(node);

                if (ExpiresVariable)
                    TimerWheel.Schedule(node);
            }

            if (IsComputingAsync(node))
            {
                lock (node)
                {
                    // TODO: see if the values is actually finished calculating..
                    long expirationTime = ExpirationTicker.Ticks() + AsyncExpiry<K, V>.ASYNC_EXPIRY;
                    SetVariableTime(node, expirationTime);
                    SetAccessTime(node, expirationTime);
                    SetWriteTime(node, expirationTime);
                }
            }
        }

        internal async Task RemoveTask(Node<K, V> node)
        {
            if (node.InEden && (Evicts || ExpiresAfterAccess))
            {
                AccessOrderEdenDeque.Remove(node);
            }
            else if (Evicts)
            {
                if (node.InMainProbation)
                {
                    AccessOrderProbationDeque.Remove(node);
                }
                else
                {
                    AccessOrderProtectedDeque.Remove(node);
                }
            }

            if (ExpiresAfterWrite)
                WriteOrderDeque.Remove(node);
            else if (ExpiresVariable)
                TimerWheel.Deschedule(node);

            MakeDead(node);
        }

    }


    public abstract class PadDrainStatus<K, V> : LocalCache<K, V>
    {
        #pragma warning disable CS0169 // the field is never used
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p10, p11, p12, p13, p14, p15, p16;
        #pragma warning restore CS0169
    }

    public abstract class DrainStatusRef<K, V> : PadDrainStatus<K, V>
    {
        protected const int IDLE = 0;
        protected const int REQUIRED = 1;
        protected const int PROCESSING_TO_IDLE = 2;
        protected const int PROCESSING_TO_REQUIRED = 3;

        private volatile int drainStatus = IDLE;

        protected bool ShouldDrainBuffers(bool delayable)
        {
            switch (DrainStatus)
            {
                case IDLE:
                    return !delayable;
                case REQUIRED:
                    return true;
                case PROCESSING_TO_IDLE:
                case PROCESSING_TO_REQUIRED:
                    return false;
                default:
                    throw new InvalidOperationException();
            }
        }

        protected int DrainStatus
        {
            get { return drainStatus; }
            set { Interlocked.Exchange(ref drainStatus, value); }
        }

        protected bool CasDrainStatus(int expect, int update)
        {
            if (Interlocked.CompareExchange(ref drainStatus, update, expect) == expect)
                return true;

            return false;
        }
    }


}
