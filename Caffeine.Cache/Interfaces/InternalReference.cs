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
    /// <summary>
    /// A weak reference that includes the entry's key reference.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class InternalReference<T> : IEquatable<T>
    {
        

        public InternalReference(T obj)
        {
            
        }

        /// <summary>
        /// Returns this reference object's referent. If this reference object
        /// has been cleared, either by the program of by the garbage collector,
        /// then this method returns NULL.
        /// </summary>
        /// <returns>The object to which the reference refers, or NULL if this reference object has been cleared.</returns>
        public abstract T Get();

        /// <summary>
        /// Returns the key that is associated to the cache entry holding this reference.
        /// If the cache holds keys strongly, this is that key instance. Otherwise the cache
        /// holds keys weakly and the <seealso cref="WeakKeyReference{K}"/> is returned
        /// </summary>
        /// <returns>The key that is associated to the cache entry</returns>
        public abstract InternalReference<T> KeyReference { get; }

        internal bool ReferenceEquals(T @object)
        {
            return Equals(@object);
        }

        public bool Equals(T other)
        {
            InternalReference<T> referent = other as InternalReference<T>;
            if (referent != null)
                return EqualityComparer<T>.Default.Equals(referent.Get(), Get());

            return false;
        }

    }
}
