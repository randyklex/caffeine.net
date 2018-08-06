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
using System.Threading;

using Caffeine.Cache.Factories;

namespace Caffeine.Cache
{
    /// <summary>
    /// An entry in the cache containing the key, value, weight, access and write metadata.
    /// The key or value may be held weakly requiring identity comparison.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    internal abstract class Node<K, V> : NodeFactory<K, V>, IAccessOrderElement<Node<K, V>>, IWriteOrder<Node<K, V>>
    {
        protected readonly object RETIRED_STRONG_KEY = default(K);
        protected readonly object DEAD_STRONG_KEY = default(K);

        public const int EDEN = 0;
        public const int PROBATION = 1;
        public const int PROTECTED = 2;

        private int weight;
        private int policyWeight;
        private long variableTime;
        private long accessTime;
        private long writeTime;
        private int queueType;

        internal Node()
        {
            weight = 1;
            policyWeight = 1;
            variableTime = 0L;
            accessTime = 0L;
            writeTime = 0L;
            queueType = EDEN;
        }

        /// <summary>
        /// Return the Key or NULL if it has been reclaimed by the garbage collector.
        /// </summary>
        /// <returns></returns>
        public virtual K Key { get; internal set; }

        /// <summary>
        /// Returns the reference that the cache is holding the entry by. This is either
        /// the key if strongly held or a <see cref="WeakKeyReference{K}"/> to that Key.
        /// </summary>
        /// <returns></returns>
        public abstract object KeyReference { get; }

        /// <summary>
        /// Returns the value or NULL if it has been reclaimed by the garbage collector.
        /// </summary>
        /// <returns></returns>
        public abstract V Value { get; set; }

        /// <summary>
        /// Returns the reference to the value. This is either the value if strongly held or a
        /// <see cref="WeakValueReference{V}"/> to that value.
        /// </summary>
        public abstract object ValueReference { get; }

        /// <summary>
        /// Returns true if the given objects are considered equivalent. A strongly
        /// held value is compared by equality and a weakly held value
        /// is compared by identity.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public abstract bool ContainsValue(V value);

        public abstract bool ContainsValue(object value);

        /// <summary>
        /// The weight of this entry, from the entry's perspective
        /// </summary>
        // TODO: Convert this to a uint?
        public virtual int Weight
        {
            get { return 1; }
            set { ; }
        }

        /// <summary>
        /// The weight of this entry, from the policy's perspective.
        /// </summary>
        public virtual int PolicyWeight
        {
            get { return 1; }
            set { ; }
        }

        /// <summary>
        /// If the entry is available in the hash-table and page replacement policy.
        /// </summary>
        public abstract bool IsAlive { get; }

        /// <summary>
        /// If the entry was removed from the hash-table and is awaiting removal from the page
        /// replacement policy.
        /// </summary>
        public abstract bool IsRetired { get; }

        /// <summary>
        /// If the entry was removed from the hash-table and the page replacement policy.
        /// </summary>
        public abstract bool IsDead { get; }

        /// <summary>
        /// Sets the node to the Retired state.
        /// </summary>
        public abstract void Retire();

        /// <summary>
        /// Sets the node to the Dead state.
        /// </summary>
        public abstract void Die();

        #region Variable Order

        /// <summary>
        /// The time that this entry was last accessed, in nanoseconds. This update may be set lazily
        /// and rely on the memory fence when the lock is released.
        /// </summary>
        public virtual long VariableTime
        {
            get { return 0L; }
            set { ; }
        }

        public virtual Node<K, V> GetPreviousInVariableOrder()
        {
            throw new NotImplementedException();
        }

        public virtual void SetPreviousInVariableOrder(Node<K, V> prev)
        {
            throw new NotImplementedException();
        }

        public virtual Node<K, V> GetNextInVariableOrder()
        {
            throw new NotImplementedException();
        }

        public virtual void SetNextInVariableOrder(Node<K, V> next)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Acccess Order

        /// <summary>
        /// Gets the queue that the entry's resides in (eden, probation or protected.
        /// </summary>
        public virtual int QueueType
        {
            get { return queueType; }
            protected set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The access time in nanoseconds.
        /// </summary>
        public virtual long AccessTime
        {
            get { return 0L; }
            set { ; }
        }

        /// <summary>
        /// Returns if the entry is in the eden or main space.
        /// </summary>
        public bool InEden
        {
            get { return QueueType == EDEN; }
        }

        /// <summary>
        /// Returns if the entry is in the main spaces' probation queue.
        /// </summary>
        public bool InMainProbation
        {
            get { return QueueType == PROBATION; }
        }

        /// <summary>
        /// Returns if the entry is in the main spaces protected queue.
        /// </summary>
        public bool InMainProtected
        {
            get { return QueueType == PROTECTED; }
        }

        public virtual Node<K, V> GetPreviousInAccessOrder()
        {
            return null;
        }

        public virtual void SetPreviousInAccessOrder(Node<K, V> prev)
        {
            throw new NotImplementedException();
        }

        public virtual Node<K, V> GetNextInAccessOrder()
        {
            return null;
        }

        public virtual void SetNextInAccessOrder(Node<K, V> next)
        {
            throw new NotImplementedException();
        }

        public void MakeMainProbation()
        {
            QueueType = PROBATION;
        }

        public void MakeMainProtected()
        {
            QueueType = PROTECTED;
        }
        
        #endregion

        /// <summary>
        /// Returns the time that this entry was last written, in nanoseconds.
        /// </summary>
        public long WriteTime
        {
            get { return 0L; }
            set { ; }
        }

        /// <summary>
        /// Atomically sets the write time to the given updated value if the current
        /// value equals the expected value and returns if the update was successful.
        /// </summary>
        /// <param name="expect">The value you expect the write time to be.</param>
        /// <param name="update">The value to set the write time to.</param>
        public bool CasWriteTime(long expect, long update)
        {
            // TODO: java implementation had a "UnsportedException".. i however actually implement, and it is not marked virtual
            return Interlocked.CompareExchange(ref writeTime, update, expect) == expect;
        }

        public virtual Node<K, V> GetPreviousInWriteOrder()
        {
            return null;
        }

        public virtual void SetPreviousInWriteOrder(Node<K, V> prev)
        {
            throw new NotImplementedException();
        }

        public virtual Node<K, V> GetNextInWriteOrder()
        {
            return null;
        }

        public virtual void SetNextInWriteOrder(Node<K, V> next)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Format("{0}=[key={1}, value={2}, weight={3}, queueType={4}, accessTime={5:d}, writeTime={6:d}, varTime={7:d}, prevInAccess={8}, nextInAccess={9}, prevInWrite={10}, nextInWrite={11}]",
                this.GetType().Name, Key, Value, Weight, QueueType, AccessTime, WriteTime, VariableTime, GetPreviousInAccessOrder() != null, GetNextInAccessOrder() != null,
                GetPreviousInWriteOrder() != null, GetNextInWriteOrder() != null);
        }
    }
}
