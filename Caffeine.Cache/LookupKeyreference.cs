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
using System.Text;

namespace Caffeine.Cache
{
    /// <summary>
    /// A short-klived adapter used for looking up an entry in the cache where the keys
    /// are weakly held. This <see cref="InternalReference{T}"/> implementation is not
    /// suitable for storing in the cache as the key is strongly held.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class LookupKeyReference<T> : InternalReference<T> where T : class
    {
        private readonly int itemHashCode;
        private readonly T item;

        public LookupKeyReference(T item)
            : base(default(T))
        {
            if (EqualityComparer<T>.Default.Equals(item, default(T)))
                throw new ArgumentNullException("item", "item cannot be null.");

            itemHashCode = item.GetHashCode();
            this.item = item;
        }

        public override T Get()
        {
            return item;
        }

        public override InternalReference<T> KeyReference
        {
            get { return this; }
        }

        public override int GetHashCode()
        {
            return itemHashCode;
        }
    }
}
