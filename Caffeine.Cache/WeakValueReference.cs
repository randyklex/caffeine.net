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
    /// <summary>
    /// The value in a cache that holds values weakly. This class retains a reference to the key
    /// in the advent that the value is reclaimed so that the entry can be removed from teh cache
    /// in constant time.
    /// </summary>
    /// <typeparam name="V"></typeparam>
    public sealed class WeakValueReference<V> : InternalReference<V> where V : class
    {
        private readonly InternalReference<V> keyReference;
        private WeakReference<V> @ref;

        public WeakValueReference(InternalReference<V> keyReference, V value)
            : base(value)
        {
            this.keyReference = keyReference;
            @ref = new WeakReference<V>(value);
        }

        public override InternalReference<V> KeyReference
        {
            get { return keyReference; }
        }

        public override V Get()
        {
            V retVal = default(V);
            @ref.TryGetTarget(out retVal);

            return retVal;
        }
    }
}
