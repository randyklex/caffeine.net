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

namespace Caffeine.Cache.Factories
{
    internal class CacheFactory<K, V>
    {
        private static readonly Lazy<CacheFactory<K, V>> lazy = new Lazy<CacheFactory<K, V>>(() => new CacheFactory<K, V>());

        private Dictionary<string, Type> cacheTypes;

        private const string strongKeysStrongValues = "SS";
        private const string strongKeysStrongValuesListener = "SSL";
        private const string strongKeysStrongValuesStatistics = "SSS";
        private const string strongKeysStrongValuesListenerStatistics = "SSLS";
        private const string strongKeysStrongValuesStatisticsEvictsBySize = "SSSMS";

        private CacheFactory()
        {
            cacheTypes = new Dictionary<string, Type>();
            cacheTypes.Add(strongKeysStrongValues, typeof(BoundedLocalCacheStrongKeyStrongValue<K, V>));
            cacheTypes.Add(strongKeysStrongValuesStatistics, typeof(BoundedLocalCacheStrongKeyStrongValueStatistics<K, V>));
            cacheTypes.Add(strongKeysStrongValuesListener, typeof(BoundedLocalCacheStrongKeyStrongValueListener<K, V>));
            cacheTypes.Add(strongKeysStrongValuesStatisticsEvictsBySize, typeof(BoundedLocalCacheStrongKeyStrongValueStatisticsEvictsBySize<K, V>));
        }

        public static CacheFactory<K, V> Instance
        {
            get { return lazy.Value; }
        }

        public BoundedLocalCache<K, V> NewBoundedLocalCache(Caffeine<K, V> builder, CacheLoader<K, V> loader, bool isAsync)
        {
            StringBuilder sb = new StringBuilder(10);
            
            // TODO: convert this to an enum with Flags..
            if (builder.IsStrongKeys)
                sb.Append("S");
            else
                sb.Append("W");

            if (builder.IsStrongValues)
                sb.Append("S");
            else
                sb.Append("I");

            if (builder.RemovalListener != null)
                sb.Append("L");

            if (builder.IsRecordingStats)
                sb.Append("S");

            if (builder.Evicts)
            {
                sb.Append("M");
                if (builder.IsWeighted)
                {
                    sb.Append("W");
                }
                else
                {
                    sb.Append("S");
                }
            }

            if (builder.DoesExpireAfterAccess || builder.DoesExpireVariable)
                sb.Append("A");

            if (builder.DoesExpireAfterWrite)
                sb.Append("W");

            if (builder.DoesRefreshAfterWrite)
                sb.Append("R");

            return (BoundedLocalCache<K, V>)Activator.CreateInstance(cacheTypes[sb.ToString()], builder, loader, isAsync);
        }
    }
}
