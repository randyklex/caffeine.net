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
using System.Linq;

namespace Caffeine.Cache
{
    internal sealed class BoundedPolicy<K, V> : IPolicy<K, V>
    {
        readonly BoundedLocalCache<K, V> cache;
        readonly Func<V, V> transformer;
        readonly bool isWeighted;

        IEviction<K, V> eviction;
        IExpiration<K, V> refreshes;
        IExpiration<K, V> afterWrite;
        IExpiration<K, V> afterAccess;
        IVariableExpiration<K, V> variable;

        public BoundedPolicy(BoundedLocalCache<K, V> cache, Func<V, V> transformer, bool isWeighted)
        {
            this.transformer = transformer;
            this.isWeighted = isWeighted;
            this.cache = cache;
        }

        public bool IsRecordingStats
        {
            get { return cache.IsRecordingStats; }
        }

        public IEviction<K, V> Eviction
        {
            get
            {
                if (!cache.Evicts)
                    return null;

                return eviction == null ? (eviction = new BoundedEviction(this)) : eviction;
            }
        }

        public IExpiration<K, V> ExpireAfterAccess
        {
            get
            {
                if (!cache.ExpiresAfterAccess)
                    return null;

                return afterAccess == null ? (afterAccess = new BoundedExpireAfterAccess(this)) : afterAccess;
            }
        }

        public IExpiration<K, V> ExpireAfterWrite
        {
            get
            {
                if (!cache.ExpiresAfterWrite)
                    return null;

                return (afterWrite == null) ? (afterWrite = new BoundedExpireAfterWrite(this)) : afterWrite;
            }
        }

        public IExpiration<K, V> RefreshAfterWrite
        {
            get
            {
                if (!cache.RefreshAfterWrite)
                    return null;

                return (refreshes == null) ? (refreshes = new BoundedRefreshAfterWrite(this)) : refreshes;
            }
        }

        public IVariableExpiration<K, V> ExpireVariably
        {
            get
            {
                if (!cache.ExpiresVariable)
                    return null;

                return (variable == null) ? (variable = new BoundedVarExpiration(this)) : variable;
            }
        }

        //public IExpiration<K, V> Expiration

        internal void RequireNonNull<V1>(V1 item)
        {
            if (EqualityComparer<V1>.Default.Equals(item, default(V1)))
                throw new ArgumentNullException("item", "item cannot be null.");
        }

        internal sealed class BoundedEviction : IEviction<K, V>
        {
            BoundedPolicy<K, V> policy;

            public BoundedEviction(BoundedPolicy<K, V> policy)
            {
                this.policy = policy;
            }

            public bool IsWeighted { get; }

            public ulong? WeightedSize
            {
                get
                {
                    if (policy.cache.Evicts && IsWeighted)
                    {
                        lock (policy.cache.evictionLock)
                        {
                            return policy.cache.AdjustedWeightedSize;
                        }
                    }

                    return null;
                }
            }

            public ulong Maximum
            {
                get { return policy.cache.Maximum; }
                set
                {
                    lock (policy.cache.evictionLock)
                    {
                        policy.cache.SetMaximum(value);
                        policy.cache.Maintenance();
                    }
                }
            }

            public int? WeightOf(K key)
            {
                policy.RequireNonNull<K>(key);

                if (!IsWeighted)
                    return null;

                Node<K, V> node = null;
                policy.cache.data.TryGetValue(policy.cache.nodeFactory.NewLookupKey(key), out node);

                if (node == null)
                    return null;

                lock (node)
                {
                    return node.Weight;
                }
            }

            public SortedDictionary<K, V> Coldest(uint limit)
            {
                return policy.cache.EvictionOrder(limit, policy.transformer, false);
            }

            public SortedDictionary<K, V> Hottest(uint limit)
            {
                return policy.cache.EvictionOrder(limit, policy.transformer, true);
            }
        }

        internal sealed class BoundedExpireAfterAccess : IExpiration<K, V>
        {
            BoundedPolicy<K, V> policy;

            public BoundedExpireAfterAccess(BoundedPolicy<K, V> policy)
            {
                this.policy = policy;
            }

            public long ExpiresAfter { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public long? AgeOf(K key)
            {
                policy.RequireNonNull<K>(key);

                K lookupKey = policy.cache.nodeFactory.NewLookupKey(key);
                Node<K, V> node = policy.cache.data[lookupKey];

                if (node == null)
                    return null;

                long age = policy.cache.ExpirationTicker.Ticks() - node.AccessTime;
                return (age > policy.cache.ExpiresAfterAccessNanos) ? new long?() : age;
            }

            public IOrderedDictionary Oldest(uint limit)
            {
                return policy.cache.ExpireAfterAccessOrder(limit, policy.transformer, true);
            }

            public IOrderedDictionary Youngest(uint limit)
            {
                return policy.cache.ExpireAfterAccessOrder(limit, policy.transformer, false);
            }
        }

        internal sealed class BoundedExpireAfterWrite : IExpiration<K, V>
        {
            BoundedPolicy<K, V> policy;

            public BoundedExpireAfterWrite(BoundedPolicy<K, V> policy)
            {
                this.policy = policy;
            }

            public long ExpiresAfter
            {
                get { return policy.cache.ExpiresAfterWriteNanos; }
                set
                {
                    policy.cache.ExpiresAfterWriteNanos = value;
                    policy.cache.ScheduleAfterWrite();
                }
            }

            public long? AgeOf(K key)
            {
                policy.RequireNonNull<K>(key);

                K lookupKey = policy.cache.nodeFactory.NewLookupKey(key);
                Node<K, V> node = policy.cache.data[key];

                if (node == null)
                    return null;

                long age = policy.cache.ExpirationTicker.Ticks() - node.WriteTime;
                return (age > policy.cache.ExpiresAfterWriteNanos) ? new long?() : age;
            }

            public IOrderedDictionary Oldest(uint limit)
            {
                return policy.cache.ExpireAfterWriteOrder(limit, policy.transformer, true);
            }

            public IOrderedDictionary Youngest(uint limit)
            {
                return policy.cache.ExpireAfterWriteOrder(limit, policy.transformer, false);
            }
        }

        internal sealed class BoundedVarExpiration : IVariableExpiration<K, V>
        {
            BoundedPolicy<K, V> policy;

            public BoundedVarExpiration(BoundedPolicy<K, V> policy)
            {
                this.policy = policy;
            }

            public long? GetExpiresAfter(K key)
            {
                policy.RequireNonNull<K>(key);

                K lookupKey = policy.cache.nodeFactory.NewLookupKey(key);
                Node<K, V> node = policy.cache.data[lookupKey];
                if (node == null)
                    return null;

                long duration = node.VariableTime - policy.cache.ExpirationTicker.Ticks();
                return (duration <= 0) ? null : (long?)duration;
            }

            // TODO: I removed "unit" from this method.. all units are in nanoseconds
            public void SetExpiresAfter(K key, ulong duration)
            {
                policy.RequireNonNull<K>(key);
                K lookupKey = policy.cache.nodeFactory.NewLookupKey(key);
                Node<K, V> node = policy.cache.data[lookupKey];

                if (node != null)
                {
                    long now = 0L;
                    lock (node)
                    {
                        now = policy.cache.ExpirationTicker.Ticks();
                        node.VariableTime = (now + Math.Min((long)duration, BoundedLocalCache<K, V>.MAXIMUM_EXPIRY));
                    }

                    policy.cache.AfterRead(node, now, false);
                }
            }

            public void Put(K key, V value, long duration)
            {
                throw new NotImplementedException();
            }

            public bool TryAdd(K key, V value, long duration)
            {
                throw new NotImplementedException();
            }

            public IOrderedDictionary Oldest(uint limit)
            {
                return policy.cache.VariableSnapshot(true, limit, policy.transformer);
            }

            public IOrderedDictionary Youngest(uint limit)
            {
                return policy.cache.VariableSnapshot(false, limit, policy.transformer);
            }
        }

        internal sealed class BoundedRefreshAfterWrite : IExpiration<K, V>
        {
            BoundedPolicy<K, V> policy;

            public BoundedRefreshAfterWrite(BoundedPolicy<K, V> policy)
            {
                this.policy = policy;
            }

            public long ExpiresAfter { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public long? AgeOf(K key)
            {
                policy.RequireNonNull<K>(key);

                K lookupKey = policy.cache.nodeFactory.NewLookupKey(key);
                Node<K, V> node = policy.cache.data[lookupKey];

                if (node == null)
                    return null;

                long age = policy.cache.ExpirationTicker.Ticks() - node.WriteTime;
                return (age > policy.cache.RefreshAfterWriteNanos) ? null : (long?)age;
            }

            public IOrderedDictionary Oldest(uint limit)
            {
                return policy.cache.ExpiresAfterWrite ? policy.ExpireAfterWrite.Oldest(limit) : SortedByWriteTime(limit, false);
            }

            public IOrderedDictionary Youngest(uint limit)
            {
                return policy.cache.ExpiresAfterWrite ? policy.ExpireAfterWrite.Youngest(limit) : SortedByWriteTime(limit, false);
            }

            private IOrderedDictionary SortedByWriteTime(uint limit, bool ascending)
            {
                // TODO: revisit this for concurrency enumerating the values of the dictionary while adding items? though synchronization may gauarantee reentrancy.
                IEnumerator<Node<K, V>> writeTimeEnumerator = ascending ? policy.cache.data.Values.OrderBy(i => i.WriteTime).GetEnumerator() : policy.cache.data.Values.OrderByDescending(i => i.WriteTime).GetEnumerator();
                return policy.cache.FixedSnapshot(writeTimeEnumerator, limit, policy.transformer);
            }
        }
    }
}
