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

namespace Caffeine.Cache
{
    public sealed class WeakKeyReference<K> : InternalReference<K> where K : class
    {
        private WeakReference<K> @ref;
        private readonly int itemHashCode;
        
        public WeakKeyReference(K key)
            : base(key)
        {
            if (EqualityComparer<K>.Default.Equals(key, default(K)))
                throw new ArgumentNullException("key", "object cannot be NULL.");

            @ref = new WeakReference<K>(key, false);

            itemHashCode = key.GetHashCode();
        }

        public override bool Equals(object other)
        {
            K casted = other as K;

            if (casted == null)
                return false;

            return ReferenceEquals(casted);
        }

        public override InternalReference<K> KeyReference
        { 
            get { return this; }
        }

        public override int GetHashCode()
        {
            return itemHashCode;
        }

        public override K Get()
        {
            K retVal = default(K);

            @ref.TryGetTarget(out retVal);

            return retVal;
        }
    }
}
