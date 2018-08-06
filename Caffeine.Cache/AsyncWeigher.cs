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

using System.Threading.Tasks;

namespace Caffeine.Cache
{
    /// <summary>
    /// A weigher for asynchronous computations. When the value is being loaded this weigher returns ZERO
    /// to indicate that the entry should not be evicted due to a size constraint. If the value is computed
    /// successfully the entry must be reinserted so that the weight is updated and the expiration
    /// timeouts reflect the value once present. This can be done safely using <see cref="ConcurrentDictionary{TKey, TValue}."/>
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public sealed class AsyncWeigher<K, V> : IWeigher<K, V>
    {
        readonly IWeigher<K, V> @delegate;

        public AsyncWeigher(IWeigher<K, V> @delegate)
        {
            this.@delegate = @delegate;
        }

        public int Weigh(K key, V value)
        {
            return @delegate.Weigh(key, value);
        }

        public int WeighAsync(K key, TaskCompletionSource<V> value)
        {
            if (value.Task.IsCompletedSuccessfully)
                return @delegate.Weigh(key, value.Task.Result);

            return 0;
        }
    }
}
