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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Caffeine.Cache
{
    public interface ITicker
    {
        long Ticks();
    }

    public abstract class Ticker
    {

#if WIN_8
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long fileTime);
#else
        private Stopwatch sw;
        private TimeSpan maxIdle = TimeSpan.FromSeconds(10);
        DateTime startTime;

        protected Ticker()
        {
            Reset();
        }

        private void Reset()
        {
            startTime = DateTime.UtcNow;
            sw = Stopwatch.StartNew();
        }

#endif

        /// <summary>
        /// Based on the platform will return the highest precision value for Ticks.
        /// </summary>
        /// <returns></returns>
        protected long GetTicks()
        {
            long rval;

#if WIN_8
            GetSystemTimePreciseAsFileTime(out rval);
#else
            if (startTime.Add(maxIdle) < DateTime.UtcNow)
                Reset();

            rval = startTime.AddTicks(sw.Elapsed.Ticks).Ticks;
#endif
            return rval;
        }
    }

    public class SystemTicker : Ticker, ITicker
    {
        private static Lazy<SystemTicker> lazy = new Lazy<SystemTicker>(() => new SystemTicker());

        private SystemTicker()
            :base()
        { }

        public static ITicker Instance
        {
            get { return lazy.Value; }
        }

        public long Ticks()
        {
            return GetTicks();
        }
    }

    public class DisabledTicker : Ticker, ITicker
    {
        private static Lazy<DisabledTicker> lazy = new Lazy<DisabledTicker>(() => new DisabledTicker());

        private DisabledTicker()
            : base()
        { }

        public static ITicker Instance
        {
            get { return lazy.Value; }
        }

        public long Ticks()
        {
            return 0L;
        }
    }
}
