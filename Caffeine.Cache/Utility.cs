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
    public class Utility
    {
        public const long NANOSECONDS_IN_SECOND = 1_000_000_000; // 1 billion
        public const long NANOSECONDS_IN_MINUTE = 60_000_000_000; // 60 billion
        public const long NANOSECONDS_IN_HOUR = 3_600_000_000_000; // 3 trillion, 600 billion
        public const long NANOSECONDS_IN_DAY = 86_400_000_000_000; // 86 trillion, 400 billion

        private const byte numOfInt32Bits = sizeof(int) * 8;
        private const byte numOfLongBits = sizeof(long) * 8;


        public static int CeilingNextPowerOfTwo(int x)
        {
            return 1 << (numOfInt32Bits - LeadingZeros(x));
        }

        public static uint CeilingNextPowerOfTwo(uint x)
        {
            return (uint)(1 << (numOfInt32Bits - LeadingZeros(x)));
        }

        public static long CeilingNextPowerOfTwo(long x)
        {
            return 1 << (numOfLongBits - LeadingZeros(x));
        }

        public static uint NumberOfSetBits(ulong i)
        {
            i = i - ((i >> 1) & 0x5555555555555555UL);
            i = (i & 0x3333333333333333L) + ((i >> 2) & 0x3333333333333333UL);
            return (uint)(unchecked(((i + (i >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        public static uint NumberOfSetBits(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (uint)(((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        public static byte LeadingZeros(int x)
        {
            if (x == 0)
                return numOfInt32Bits;

            // smear 1 across from the most significant bit set
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;

            //count the ones
            x -= x >> 1 & 0x55555555;
            x = (x >> 2 & 0x33333333) + (x & 0x33333333);
            x = (x >> 4) + x & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;

            return (byte)(numOfInt32Bits - (x & 0x0000001f)); //subtract # of 1s from 32
        }

        public static int LeadingZeros(long x)
        {
            const int numLongBits = sizeof(long) * 8; //compile time constant

            if (x == 0)
                return numLongBits;

            // smear 1 across from the most significant bit set
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            x |= x >> 32;

            //count the ones
            x -= x >> 1 & 0x5555555555555555;
            x = (x >> 2 & 0x3333333333333333) + (x & 0x3333333333333333);
            x = (x >> 4) + x & 0x0f0f0f0f0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            x += x >> 32;

            return (int)(numLongBits - (x & 0x0000003f)); //subtract # of 1s from 64
        }
    }
}
