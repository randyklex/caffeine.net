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

using Caffeine.Cache.Interfaces;

namespace Caffeine.Cache
{
    // TODO: This was a conversion from an interface of PeekingIterator in Java, to PeekingEnumerator abstract.
    // I converted because the interface actually had some implementation so this became an abstract class.
    public abstract class PeekingIterator<T> : IPeekingEnumerator<T>
    {
        public T Current => throw new System.NotImplementedException();

        object IEnumerator.Current => throw new System.NotImplementedException();

        ~PeekingIterator()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public abstract bool MoveNext();

        public abstract T Peek();

        public void Reset()
        {
            throw new System.NotImplementedException();
        }

        private void Dispose(bool disposing)
        { }
    }

    public class ConcatPeekingIterator<T> : PeekingIterator<T>
    {
        IPeekingEnumerator<T> first;
        IPeekingEnumerator<T> second;

        public ConcatPeekingIterator(IPeekingEnumerator<T> first, IPeekingEnumerator<T> second)
        {
            this.first = first;
            this.second = second;
        }

        public override bool MoveNext()
        {
            if (first.MoveNext())
                return true;
            else
                return second.MoveNext();
        }

        public override T Peek()
        {
            T rval = first.Peek();

            if (rval == null)
                rval = second.Peek();

            return rval;
        }
    }

    public class ComparingPeekingIterator<T> : PeekingIterator<T>
    {
        IPeekingEnumerator<T> first;
        IPeekingEnumerator<T> second;
        IComparer<T> comparer;

        public ComparingPeekingIterator(IPeekingEnumerator<T> first, IPeekingEnumerator<T> second, IComparer<T> comparer)
        {
            this.first = first;
            this.second = second;
            this.comparer = comparer;
        }

        public override bool MoveNext()
        {
            // TODO: Look at this method in the PeekingIterator<E> class in the Java source. I really think these lines are a bug, unless there's some Java (fuck-me-magic) going on.
            //if (first.MoveNext())
            //    return true;
            //else if (second.MoveNext())
            //    return true;

            T obj1 = first.Peek();
            T obj2 = second.Peek();

            bool isFirstObjGreaterOrEqual = (comparer.Compare(obj1, obj2) >= 0);
            return isFirstObjGreaterOrEqual ? first.MoveNext() : second.MoveNext();
        }

        public override T Peek()
        {
            T obj1 = first.Peek();
            T obj2 = second.Peek();

            bool isFirstObjGreaterOrEqual = (comparer.Compare(obj1, obj2) >= 0);
            return isFirstObjGreaterOrEqual ? first.Peek() : second.Peek();
        }
    }

    public static class PeekingIteratorFactory<T>
    {
        static IPeekingEnumerator<T> Concat(IPeekingEnumerator<T> first, IPeekingEnumerator<T> second)
        {
            return new ConcatPeekingIterator<T>(first, second);
        }

        static IPeekingEnumerator<T> Comparing(IPeekingEnumerator<T> first, IPeekingEnumerator<T> second, IComparer<T> comparer)
        {
            return new ComparingPeekingIterator<T>(first, second, comparer);
        }
    }
}
