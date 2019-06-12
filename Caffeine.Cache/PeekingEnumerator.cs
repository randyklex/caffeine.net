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
using System.Collections;
using System.Collections.Generic;

namespace Caffeine.Cache
{
    internal abstract class PeekingEnumerator<T, T1> : IEnumerator<T1> where T : AbstractLinkedDeque<T1>
    {
        protected T linkedDeque;
        protected T1 current = default(T1);

        public PeekingEnumerator(T linkedDeque)
        {
            this.linkedDeque = linkedDeque;
            current = linkedDeque.Peek();
        }

        ~PeekingEnumerator()
        {
            Dispose(false);
        }

        public T1 Current
        {
            get { return current; }
        }

        object IEnumerator.Current
        {
            get { return current; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public abstract bool MoveNext();

        public void Reset()
        {
            throw new NotImplementedException();
        }

        private void Dispose(bool disposing)
        { }

        public abstract T1 Peek();
    }

    internal class AscendingPeekingEnumerator<T, T1> : PeekingEnumerator<T, T1> where T : AbstractLinkedDeque<T1>
    {
        public AscendingPeekingEnumerator(T linkedDeque)
            :base(linkedDeque)
        { }

        public override bool MoveNext()
        {
            if (EqualityComparer<T1>.Default.Equals(current, default(T1)))
                current = linkedDeque.First;
            else
                current = linkedDeque.GetNext(current);

            if (current != null)
                return true;

            return false;
        }

        public override T1 Peek()
        {
            throw new NotImplementedException();
        }
    }

    internal class DescendingPeekingEnumerator<T, T1> : PeekingEnumerator<T, T1> where T : AbstractLinkedDeque<T1>
    {
        public DescendingPeekingEnumerator(T linkedDeque)
            : base(linkedDeque)
        { }

        public override bool MoveNext()
        {
            if (EqualityComparer<T1>.Default.Equals(current, default(T1)))
                current = linkedDeque.Last;
            else
                current = linkedDeque.GetPrevious(current);

            if (current != null)
                return true;

            return false;
        }

        public override T1 Peek()
        {
            throw new NotImplementedException();
        }
    }
}
