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

using Xunit;

using Caffeine.Cache;

namespace Caffeine.Tests
{
    public class UtilityTests
    {

        [Fact]
        public void PowerOfTwo_1()
        {
            Assert.Equal<int>(2, Utility.CeilingNextPowerOfTwo(1));
        }

        [Fact]
        public void PowerOfTwo_2()
        {
            Assert.Equal<int>(4, Utility.CeilingNextPowerOfTwo(2));
        }

        [Fact]
        public void PowerOfTwo_4()
        {
            Assert.Equal<int>(8, Utility.CeilingNextPowerOfTwo(4));
        }

        [Fact]
        public void PowerOfTwo_5()
        {
            Assert.Equal<int>(8, Utility.CeilingNextPowerOfTwo(5));
        }

        [Fact]
        public void PowerOfTwo_8()
        {
            Assert.Equal<int>(16, Utility.CeilingNextPowerOfTwo(8));
        }

        [Fact]
        public void PowerOfTwo_11()
        {
            Assert.Equal<int>(16, Utility.CeilingNextPowerOfTwo(11));
        }

        [Fact]
        public void PowerOfTwo_33()
        {
            Assert.Equal<int>(64, Utility.CeilingNextPowerOfTwo(33));
        }

        [Fact]
        public void PowerOfTwo_1_billion()
        {
            int result = Utility.CeilingNextPowerOfTwo(1073741825);
            Assert.Equal<int>(-2147483648, result);
        }

        [Fact]
        public void LeadingZeros32Bit_1()
        {
            Assert.Equal<int>(31, Utility.LeadingZeros(1));
        }

        [Fact]
        public void LeadingZeros32Bit_16()
        {
            Assert.Equal<int>(27, Utility.LeadingZeros(16));
        }

        [Fact]
        public void LeadingZeros64Bit_1()
        {
            Assert.Equal<long>(63, Utility.LeadingZeros(1L));
        }

        [Fact]
        public void LeadingZeros64Bit_256()
        {
            Assert.Equal<long>(55, Utility.LeadingZeros(256L));
        }
    }
}
