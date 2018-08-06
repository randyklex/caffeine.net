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

namespace Caffeine.Cache
{
    public class DisabledBuffer<T> : Buffer<T>
    {
        private static readonly Lazy<DisabledBuffer<T>> lazy = new Lazy<DisabledBuffer<T>>(() => new DisabledBuffer<T>());

        private DisabledBuffer()
            : base()
        { }

        public static DisabledBuffer<T> Instance
        {
            get { return lazy.Value; }
        }

        public override uint Count()
        {
            return 0;
        }

        public override void DrainTo(Action<T> consumer)
        {
            return;
        }

        public override OfferStatusCodes Offer(T element)
        {
            return OfferStatusCodes.SUCCESS;
        }

        public override uint Reads()
        {
            return 0;
        }

        public override uint Writes()
        {
            return 0;
        }
    }
}
