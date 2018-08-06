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
using System.Threading.Tasks;

namespace Caffeine.Cache
{
    // TODO: this class implements Serializable.. why? Do we need it?
    public sealed class BoundedWeigher<K, V> : IWeigher<K, V>
    {
        readonly IWeigher<K, V> @delegate;

        public BoundedWeigher(IWeigher<K, V> @delegate)
        {
            if (EqualityComparer<IWeigher<K, V>>.Default.Equals(@delegate, default(IWeigher<K, V>)))
                throw new ArgumentNullException("delegate", "delegate cannot be null.");

            this.@delegate = @delegate;
        }

        public int Weigh(K key, V value)
        {
            // TODO: diff - using uint and don't do a check for a positive number..
            return @delegate.Weigh(key, value); 
        }

        public int WeighAsync(K key, TaskCompletionSource<V> value)
        {
            throw new NotImplementedException();
        }
    }
}
