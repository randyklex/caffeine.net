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
using System.Threading.Tasks;


namespace Caffeine.Cache
{
    /// <summary>
    /// A removal listener that asynchronously forwards the value from a <see cref="Task{TResult}"/>
    /// if successful to the user-supplied removal listener.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    internal sealed class AsyncRemovalListener<K, V> : IRemovalListener<K, V>
    {
        readonly IRemovalListener<K, V> @delegate;

        public AsyncRemovalListener(IRemovalListener<K, V> @delegate)
        {
            this.@delegate = @delegate;
        }

        public void OnRemoval(K key, V value, RemovalCause cause)
        {
            @delegate.OnRemoval(key, value, cause);
        }

        public void OnRemovalAsync(K key, Task<V> computableValue, RemovalCause cause)
        {
            if (computableValue != null)    
                computableValue.ContinueWith(v => @delegate.OnRemoval(key, v.Result, cause));
        }
    }
}
