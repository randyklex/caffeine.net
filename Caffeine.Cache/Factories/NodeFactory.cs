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
using System.Text;

namespace Caffeine.Cache.Factories
{
    // TODO: this basically makes it so that any object stored in the cache has to have a reference type as the key..
    internal abstract class NodeFactory<K, V>
    {
        public NodeFactory()
        { }

        internal abstract Node<K, V> NewNode(K key, V value, int weight, long expire);

        internal virtual object NewReferenceKey(K key)
        {
            return key;
        }

        internal virtual K NewLookupKey(K key)
        {
            return key;
        }

        public virtual bool WeakValues { get { return false; } }

        public static NodeFactory<K, V> NewFactory(Caffeine<K, V> builder, bool isAsync)
        {
            StringBuilder sb = new StringBuilder(10);
            if (builder.IsStrongKeys)
            {
                sb.Append('P');
            }
            else
            {
                sb.Append('F');
            }

            return new NodeStrongKeyStrongValue<K, V>();
        }
    }
}
