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

using System.Collections.Generic;

namespace Caffeine.Cache
{
    internal class NodeStrongKeyStrongValue<K, V> : Node<K, V>
    {
        K key;
        V nodeValue;

        public NodeStrongKeyStrongValue()
        { }

        private NodeStrongKeyStrongValue(K key, V value, int weight, long now)
        {
            this.key = key;
            this.nodeValue = value;
        }

        private NodeStrongKeyStrongValue(object keyReference, V value, int weight, long now)
        {
            this.key = (K)keyReference;
            this.nodeValue = value;
        }

        public override K Key
        {
            get { return key; }
            internal set { key = value; }
        }

        public override object KeyReference
        {
            get { return key; }
        }

        public override V Value
        {
            get { return nodeValue; }
            set { nodeValue = value; }
        }

        public override object ValueReference
        {
            get { return (object)nodeValue; }
        }

        public override bool ContainsValue(V value)
        {
            return (EqualityComparer<V>.Default.Equals(value, nodeValue));
        }

        public override void Retire()
        {
            key = (K)RETIRED_STRONG_KEY;
        }

        public override bool IsDead
        {
            get { return KeyReference == DEAD_STRONG_KEY; }
        }

        public override bool IsAlive
        {
            get
            {
                object key = KeyReference;
                return (key != RETIRED_STRONG_KEY) && (key != DEAD_STRONG_KEY);
            }
        }

        public override bool IsRetired
        {
            get { return KeyReference == RETIRED_STRONG_KEY; }
        }

        public override void Die()
        {
            nodeValue = default(V);
            key = (K)DEAD_STRONG_KEY;
        }

        public override bool ContainsValue(object value)
        {
            return value.Equals(nodeValue);
        }

        internal override Node<K, V> NewNode(K key, V value, int weight, long expire)
        {
            return new NodeStrongKeyStrongValue<K, V>(key, value, weight, expire);
        }
    }
}
