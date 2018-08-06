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

using System.Collections.Generic;

namespace Caffeine.Cache
{
    public sealed class WriteOrderDeque<T> : AbstractLinkedDeque<T> where T : IWriteOrder<T>
    {
        public WriteOrderDeque()
        { }

        public override bool Contains(T element)
        {
            return (element.GetPreviousInWriteOrder() != null) || (element.GetNextInWriteOrder() != null) || EqualityComparer<T>.Default.Equals(element, first);
        }

        public override bool Remove(T element)
        {
            if (Contains(element))
            {
                Unlink(element);
                return true;
            }

            return false;
        }

        public override T GetPrevious(T element)
        {
            return element.GetPreviousInWriteOrder();
        }

        public override void SetPrevious(T element, T previousElement)
        {
            element.SetPreviousInWriteOrder(previousElement);
        }

        public override T GetNext(T element)
        {
            return element.GetNextInWriteOrder();
        }

        public override void SetNext(T element, T nextElement)
        {
            element.SetNextInWriteOrder(nextElement);
        }
    }
}
