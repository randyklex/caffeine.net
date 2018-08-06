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

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache
{
    internal class DisabledCacheWriter<K, V> : ICacheWriter<K, V>
    {
        private static Lazy<DisabledCacheWriter<K, V>> instance = new Lazy<DisabledCacheWriter<K, V>>(() => new DisabledCacheWriter<K, V>());

        private DisabledCacheWriter()
        { }

        public static DisabledCacheWriter<K, V> Instance { get { return instance.Value; } }

        public void Delete(K key, V value, RemovalCause cause)
        {
            return;
        }

        public void Write(K key, V value)
        {
            return;
        }
    }
}
