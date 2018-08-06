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

namespace Caffeine.Cache
{
    public sealed class AccessOrderDeque<T> : AbstractLinkedDeque<T> where T : IAccessOrderElement<T>
    {
        public AccessOrderDeque()
        { }

        // TODO: the original JAVA had a Contains(object o).. but I don't understand that if we have the type parameter for generics.
        // bool Contains(object o) { }

        public override bool Contains(T element)
        {
            return (element.GetPreviousInAccessOrder() != null) || (element.GetNextInAccessOrder() != null) || EqualityComparer<T>.Default.Equals(element, first);
        }

        // TODO: the original JAVA had a Remove(object o).. same thing - why with "object" when you know the type parameter.
        // bool Remove(object o) { }

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
            return element.GetPreviousInAccessOrder();
        }

        public override void SetPrevious(T element, T previous)
        {
            element.SetPreviousInAccessOrder(previous);
        }

        public override T GetNext(T element)
        {
            return element.GetNextInAccessOrder();
        }

        public override void SetNext(T element, T next)
        {
            element.SetNextInAccessOrder(next);
        }
    }
}
