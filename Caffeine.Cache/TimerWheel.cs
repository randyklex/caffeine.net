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
using System.Collections.Specialized;
using System.Text;

namespace Caffeine.Cache
{
    /// <summary>
    /// A hierarchical timer wheel to add, remove and fire expiration events in amortized O(1)
    /// time. The expiration events are deferred until the timer is advanced, which is performed
    /// as part of the cache's maintenance cycle.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public sealed class TimerWheel<K, V>
    {

        private static readonly int[] BUCKETS = { 64, 64, 32, 4, 1 };
        private static readonly long[] SPANS =
        {
            Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_SECOND),
            Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_MINUTE),
            Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_HOUR),
            Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_DAY),
            BUCKETS[3] * Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_DAY),
            BUCKETS[3] * Utility.CeilingNextPowerOfTwo(Utility.NANOSECONDS_IN_DAY)
        };

        private static readonly int[] SHIFT =
        {
            sizeof(long) - Utility.LeadingZeros(SPANS[0] - 1),
            sizeof(long) - Utility.LeadingZeros(SPANS[1] - 1),
            sizeof(long) - Utility.LeadingZeros(SPANS[2] - 1),
            sizeof(long) - Utility.LeadingZeros(SPANS[3] - 1),
            sizeof(long) - Utility.LeadingZeros(SPANS[4] - 1)
        };

        private long nanos;

        private readonly BoundedLocalCache<K, V> cache;
        private readonly Node<K, V>[][] wheel;

        public TimerWheel(BoundedLocalCache<K, V> cache)
        {

            this.cache = cache ?? throw new ArgumentNullException("cache", "cache cannot be null.");

            wheel = new Node<K, V>[BUCKETS.Length][];

            for (int i = 0; i < wheel.Length; i++)
            {
                wheel[i] = new Node<K, V>[BUCKETS[i]];
                for (int j = 0; j < wheel[i].Length; j++)
                {
                    wheel[i][j] = new Sentinel<K, V>();
                }
            }
        }

        /// <summary>
        /// Advances the timer and evicts entries that have expired.
        /// </summary>
        /// <param name="currentTimeoutNanoseconds">The current time in nanoseconds</param>
        public void Advance(long currentTimeoutNanoseconds)
        {
            long previousTimeout = nanos;

            try
            {
                nanos = currentTimeoutNanoseconds;

                for (int i = 0; i < SHIFT.Length; i++)
                {
                    long previousTicks = (previousTimeout >> SHIFT[i]);
                    long currentTicks = (currentTimeoutNanoseconds >> SHIFT[i]);

                    if ((currentTimeoutNanoseconds - previousTimeout) <= 0L)
                        break;

                    Expire(i, previousTicks, currentTicks);
                }
            }
            catch (Exception e)
            {
                nanos = previousTimeout;
                throw;
            }
        }

        /// <summary>
        /// Expires entries or reschedules into the proper bucket if still active.
        /// </summary>
        /// <param name="index">The wheel being operated on</param>
        /// <param name="previousTicks">the previous number of ticks</param>
        /// <param name="currentTicks">the current number of ticks</param>
        private void Expire(int index, long previousTicks, long currentTicks)
        {
            Node<K, V>[] timerWheel = wheel[index];

            int start, end;

            if ((currentTicks - previousTicks) >= timerWheel.Length)
            {
                end = timerWheel.Length;
                start = 0;
            }
            else
            {
                start = (int)(previousTicks & (SPANS[index] - 1));
                end = 1 + (int)(currentTicks & (SPANS[index] - 1));
            }

            int mask = timerWheel.Length - 1;
            for (int i = start; i < end; i++)
            {
                Node<K, V> sentinel = timerWheel[(i & mask)];
                Node<K, V> prev = sentinel.GetPreviousInVariableOrder();
                Node<K, V> node = sentinel.GetNextInVariableOrder();

                sentinel.SetPreviousInVariableOrder(sentinel);
                sentinel.SetNextInVariableOrder(sentinel);

                while (node != sentinel)
                {
                    Node<K, V> next = node.GetNextInVariableOrder();
                    node.SetPreviousInVariableOrder(null);
                    node.SetNextInVariableOrder(null);

                    try
                    {
                        if (((node.VariableTime - nanos) > 0) || !cache.EvictEntry(node, RemovalCause.EXPIRED, nanos))
                        {
                            Node<K, V> newSentinel = FindBucket(node.VariableTime);
                            Link(newSentinel, node);
                        }
                    }
                    catch (Exception)
                    {
                        node.SetPreviousInVariableOrder(sentinel.GetPreviousInVariableOrder());
                        node.SetNextInVariableOrder(next);

                        sentinel.GetPreviousInVariableOrder().SetNextInVariableOrder(node);
                        sentinel.SetPreviousInVariableOrder(prev);

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Schedules a timer event for the node.
        /// </summary>
        /// <param name="node"></param>
        internal void Schedule(Node<K, V> node)
        {
            Node<K, V> sentinel = FindBucket(node.VariableTime);
            Link(sentinel, node);
        }

        /// <summary>
        /// Rescheduels an active timer event for the node.
        /// </summary>
        /// <param name="node"></param>
        internal void Reschedule(Node<K, V> node)
        {
            if (node.GetNextInVariableOrder() != null)
            {
                UnLink(node);
                Schedule(node);
            }
        }

        /// <summary>
        /// Removes a timer event for this entry if present.
        /// </summary>
        /// <param name="node"></param>
        internal void Deschedule(Node<K, V> node)
        {
            UnLink(node);
            node.SetNextInVariableOrder(null);
            node.SetPreviousInVariableOrder(null);
        }

        /// <summary>
        /// Determines the bucket that the timer event should be added to.
        /// </summary>
        /// <param name="time">The time when the event fires</param>
        /// <returns>The sentinel at the head of the bucket.</returns>
        private Node<K, V> FindBucket(long time)
        {
            long duration = time - nanos;
            int length = wheel.Length - 1;
            for (int i = 0; i < length; i++)
            {
                if (duration < SPANS[i + 1])
                {
                    int ticks = (int)(time >> SHIFT[i]);
                    int index = ticks & (wheel[i].Length - 1);
                    return wheel[i][index];
                }
            }

            return wheel[length][0];
        }

        /// <summary>
        /// Adds the entry at the tail of the bucket's list.
        /// </summary>
        /// <param name="sentinel"></param>
        /// <param name="node"></param>
        private void Link(Node<K, V> sentinel, Node<K, V> node)
        {
            node.SetPreviousInVariableOrder(sentinel.GetPreviousInVariableOrder());
            node.SetNextInVariableOrder(sentinel);

            sentinel.GetPreviousInVariableOrder().SetNextInVariableOrder(node);
            sentinel.SetPreviousInVariableOrder(node);
        }

        /// <summary>
        /// Removes the entry from its bucket, if scheduled.
        /// </summary>
        /// <param name="node"></param>
        private void UnLink(Node<K, V> node)
        {
            Node<K, V> next = node.GetNextInVariableOrder();
            if (next != null)
            {
                Node<K, V> prev = node.GetPreviousInVariableOrder();
                next.SetPreviousInVariableOrder(prev);
                prev.SetNextInVariableOrder(next);
            }
        }

        /// <summary>
        /// Returns an unmodifiable snapshot roughly ordered by the expiration time. The wheels are
        /// evaluated in order, but the timers that fall within the bucket's range are not sorted.
        /// Beware that obtaining the mappings is NOT a constant-time operation.
        /// </summary>
        /// <param name="ascending">The direction</param>
        /// <param name="limit">The maximum number of entries to return</param>
        /// <param name="transformer">a function that unwraps the value</param>
        /// <returns>an unmodifiable snapshot in the desired order</returns>
        // TODO: Need a IReadonlyDictionary here.
        public OrderedDictionary Snapshot(bool ascending, int limit, Func<V, V> transformer)
        {
            // TODO: The original java implementation had a LinkedHashMap. Need to implement that or understand what it means if we don't.
            OrderedDictionary rval = new OrderedDictionary(Math.Min(limit, cache.Count));

            int startLevel = ascending ? 0 : wheel.Length - 1;

            for (int i= 0; i < wheel.Length; i++)
            {
                int indexOffset = ascending ? i : -i;
                int index = startLevel + indexOffset;

                int ticks = (int)(nanos >> SHIFT[index]);
                int bucketMask = wheel[index].Length - 1;
                int startBucket = (ticks & bucketMask) + (ascending ? 1 : 0);

                for (int j = 0; j < wheel[index].Length; j++)
                {
                    int bucketOffset = ascending ? j : -j;
                    Node<K, V> sentinel = wheel[index][(startBucket + bucketOffset) & bucketMask];

                    for (Node<K, V> node = Traverse(ascending, sentinel); node != sentinel; node = Traverse(ascending, node))
                    {
                        if (rval.Count >= limit)
                            break;

                        K key = node.Key;
                        V value = transformer(node.Value);

                        if (key != null && value != null && node.IsAlive)
                            rval.Add(key, value);
                    }
                }
            }

            return rval;
        }

        private Node<K, V> Traverse(bool ascending, Node<K, V> node)
        {
            return ascending ? node.GetNextInVariableOrder() : node.GetPreviousInVariableOrder();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < wheel.Length; i++)
            {
                Dictionary<int, List<K>> buckets = new Dictionary<int, List<K>>();
                for (int j = 0; j< wheel[i].Length; j++)
                {
                    List<K> events = new List<K>();
                    for (Node<K, V> node = wheel[i][j].GetNextInVariableOrder(); node != wheel[i][j]; node = node.GetNextInVariableOrder())
                    {
                        events.Add(node.Key);
                    }

                    if (events.Count > 0)
                        buckets.Add(j, events);
                }

                builder.Append("Wheel #").Append(i + 1).Append(": ").Append(buckets).Append("\n");
            }

            return builder.Remove(builder.Length -1, 1).ToString();
        }
    }
}
