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

namespace Caffeine.Cache
{
    /// <summary>
    /// An eviction policy that supports no boundings.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public sealed class UnboundedPolicy<K, V> : IPolicy<K, V>
    {
        public UnboundedPolicy(bool isRecordingStats)
        {
            IsRecordingStats = isRecordingStats;
        }

        public bool IsRecordingStats { get; private set; }

        public IEviction<K, V> Eviction {  get { return null; } }

        public IExpiration<K, V> ExpireAfterAccess {  get { return null; } }

        public IExpiration<K, V> ExpireAfterWrite { get { return null; } }

        public IExpiration<K, V> RefreshAfterWrite { get { return null; } }

        public IVariableExpiration<K, V> ExpireVariably { get { return null; } }
    }
}
