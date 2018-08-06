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

namespace Caffeine.Cache
{
    internal class Sentinel<K, V> : Node<K, V>
    {
        Node<K, V> prev;
        Node<K, V> next;

        public Sentinel()
        {
            prev = next = this;
        }

        public override Node<K, V> GetPreviousInVariableOrder()
        {
            return prev;
        }

        public override void SetPreviousInVariableOrder(Node<K, V> prev)
        {
            this.prev = prev;
        }

        public override Node<K, V> GetNextInVariableOrder()
        {
            return next;
        }

        public override void SetNextInVariableOrder(Node<K, V> next)
        {
            this.next = next;
        }

        public override K Key
        {
            get { return default(K); }
        }

        public override object KeyReference
        {
            get { throw new InvalidOperationException(); }
        }

        public override V Value
        {
            get { return default(V); }
            set {; }
        }

        public override object ValueReference
        {
            get { throw new InvalidOperationException(); }
        }

        public override bool ContainsValue(V value)
        {
            return false;
        }

        public override bool IsAlive
        {
            get { return false; }
        }

        public override bool IsRetired
        {
            get { return false; }
        }

        public override bool IsDead
        {
            get { return false; }
        }

        public override void Die() { return; }

        public override void Retire() { return; }

        public override void SetPreviousInAccessOrder(Node<K, V> prev) { throw new NotImplementedException(); }

        public override void SetNextInAccessOrder(Node<K, V> next) { throw new NotImplementedException(); }

        public override void SetPreviousInWriteOrder(Node<K, V> prev) { throw new NotImplementedException(); }

        public override void SetNextInWriteOrder(Node<K, V> next) { throw new NotImplementedException(); }

        public override bool ContainsValue(object value)
        {
            return false;
        }

        internal override Node<K, V> NewNode(K key, V value, int weight, long expire)
        {
            return new Sentinel<K, V>();
        }
    }
}
