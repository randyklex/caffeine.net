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
using System.Threading;

namespace Caffeine.Cache
{
    /// <summary>
    /// This class provides a skeletal implementation of the <see cref="ILinkedDeque{T}"/> interface
    /// to minimize the effort required to implement this interface
    /// </summary>
    /// <typeparam name="T">The type of elements held in this collection</typeparam>
    public abstract class AbstractLinkedDeque<T> : ILinkedDeque<T>, IEnumerable<T>
    {
        // TODO: the methods immediately below I'm ommiting from this implementation.. I think they are methods from the java base class that this was originally written against.
        // getFirst()
        // getLast()
        // element()
        // poll()
        // pollFirst()
        // pollLast()
        // removeFirstOccurrence

        protected T first;
        protected T last;
        private int count;

        protected AbstractLinkedDeque()
            : base()
        {
            count = 0;
        }

        /// <summary>
        /// Links the element to the front of the deque so that it becomes the first element.
        /// </summary>
        /// <param name="element"></param>
        public void LinkFirst(T element)
        {
            T originalFirst = first;
            first = element;

            /* 
             * To avoid boxing, the best way to compare generics for equality is with EqualityComparer<T>.Default.
             * This respects IEquatable<T> (without boxing) as well as object.Equals, and handles all
             * the Nullable<T> "lifted" nuances.
             * https://stackoverflow.com/questions/65351/null-or-default-comparison-of-generic-argument-in-c-sharp
             */
            if (EqualityComparer<T>.Default.Equals(originalFirst, default(T)))
            {
                // If the element that was first, is NULL, then the first 
                // and last become the same element.
                last = element;
            }
            else
            {
                SetPrevious(originalFirst, element);
                SetNext(element, originalFirst);
            }

            Interlocked.Increment(ref count);
        }

        /// <summary>
        /// Links the element to the back of the deque so that it becomes the last element.
        /// </summary>
        /// <param name="element"></param>
        public void LinkLast(T element)
        {
            T l = last;
            last = element;

            if (EqualityComparer<T>.Default.Equals(l, default(T)))
            {
                first = element;
            }
            else
            {
                SetNext(l, element);
                SetPrevious(element, l);
            }

            Interlocked.Increment(ref count);
        }

        /// <summary>
        /// Unlinks the non-null first element.
        /// </summary>
        /// <returns></returns>
        public T UnlinkFirst()
        {
            T f = first;
            T next = GetNext(f);
            SetNext(f, default(T));

            first = next;
            if (EqualityComparer<T>.Default.Equals(next, default(T)))
            {
                last = default(T);
            }
            else
            {
                SetPrevious(next, default(T));
            }

            Interlocked.Decrement(ref count);

            return f;
        }

        /// <summary>
        /// Unlinks the non-null last element.
        /// </summary>
        /// <returns></returns>
        public T UnlinkLast()
        {
            T l = last;
            T prev = GetPrevious(l);
            SetPrevious(l, default(T));

            last = prev;
            if (EqualityComparer<T>.Default.Equals(prev, default(T)))
            {
                first = default(T);
            }
            else
            {
                SetNext(prev, default(T));
            }

            Interlocked.Decrement(ref count);

            return l;
        }

        /// <summary>
        /// Unlinks the non-null element
        /// </summary>
        /// <param name="element"></param>
        public void Unlink(T element)
        {
            T prev = GetPrevious(element);
            T next = GetNext(element);

            if (EqualityComparer<T>.Default.Equals(prev, default(T)))
            {
                first = next;
            }
            else
            {
                SetNext(prev, next);
                SetPrevious(element, default(T));
            }

            if (EqualityComparer<T>.Default.Equals(next, default(T)))
            {
                last = prev;
            }
            else
            {
                SetPrevious(next, prev);
                SetNext(element, default(T));
            }

            Interlocked.Decrement(ref count);
        }

        public bool IsEmpty
        {
            get { return EqualityComparer<T>.Default.Equals(first, default(T)); }
        }

        public T First
        {
            get { return first; }
        }

        public T Last
        {
            get { return last; }
        }

        // TODO: This method is different than the Java version. the Java version had Size(), and here we're using Count to be consistent with .NET framework and the ICollection<T> interface
        /// <summary>
        /// Beware that, unlike in most collections, this method is NOT a constant-time operation. This is O(n)
        /// </summary>
        /// <returns></returns>
        int ILinkedDeque<T>.Count
        {
            get { return CountImpl(); }
        }

        int ICollection<T>.Count
        {
            get { return CountImpl(); }
        }

        private int CountImpl()
        {
            // TODO: original java impelmentation do this with a loop. I'm counting when items are added or removed.
            //int count = 0;

            //for (T element = first; !EqualityComparer<T>.Default.Equals(element, default(T)); element = GetNext(element))
            //    count++;

            return count;
        }

        public bool IsReadOnly => throw new NotImplementedException();

        public void CheckNotEmpty()
        {
            if (IsEmpty)
                // TODO: Revisit this.. Java uses NoSuchElementException to indicate that the list is empty while .NET uses IEnumerator returns false if there are no elements.. How does Caffeine use this?
                throw new ArgumentOutOfRangeException("The list is empty.");
        }

        public void Clear()
        {
            for (T element = first; !EqualityComparer<T>.Default.Equals(element, default(T)); )
            {
                T next = GetNext(element);
                SetPrevious(element, default(T));
                SetNext(element, default(T));
                element = next;
            }

            first = last = default(T);
        }

        // TODO: The original Java version had "object" as the type, but it seems ridiculous given we're a generic class. Why?
        public abstract bool Contains(T o);

        public virtual T GetNext(T element)
        {
            CheckNotEmpty();
            return PeekFirst();
        }

        public virtual T GetPrevious(T element)
        {
            throw new NotImplementedException();
        }

        public bool IsFirst(T element)
        {
            if (EqualityComparer<T>.Default.Equals(element, default(T)))
                return false;

            return EqualityComparer<T>.Default.Equals(element, first);
        }

        public bool IsLast(T element)
        {
            if (EqualityComparer<T>.Default.Equals(element, default(T)))
                return false;

            return EqualityComparer<T>.Default.Equals(element, last);
        }

        public void MoveToFront(T element)
        {
            if (!EqualityComparer<T>.Default.Equals(element, first))
            {
                Unlink(element);
                LinkFirst(element);
            }
        }

        public void MoveToBack(T element)
        {
            if (!EqualityComparer<T>.Default.Equals(element, last))
            {
                Unlink(element);
                LinkLast(element);
            }
        }

        public virtual void SetNext(T element, T nextElement)
        {
            // TODO: why not make this abstract and force the inheritor to override?
            throw new NotImplementedException();
        }

        public virtual void SetPrevious(T element, T previousElement)
        {
            // TODO: why not make this abstract and force the inheritor to override?
            throw new NotImplementedException();
        }

        public void Add(T item)
        {
            OfferLast(item);
            // TODO: if the LinkedDeque already contains the item it won't move it to the last.. so what to do!!??
        }

        public void AddFirst(T item)
        {
            if (!OfferFirst(item))
                throw new ArgumentException("item already exists.");
        }

        public void AddLast(T item)
        {
            if (!OfferLast(item))
                throw new ArgumentException("item already exists.");
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public virtual bool Remove(T item)
        {
            Unlink(item);
            return true;
        }

        public T Remove()
        {
            return RemoveFirst();
        }

        public T RemoveFirst()
        {
            CheckNotEmpty();
            return UnlinkFirst();
        }

        public T RemoveLast()
        {
            CheckNotEmpty();
            return UnlinkLast();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new AscendingPeekingEnumerator<AbstractLinkedDeque<T>, T>(this);
        }

        public IEnumerator GetEnumerator()
        {
            return new AscendingPeekingEnumerator<AbstractLinkedDeque<T>, T>(this);
        }

        public IEnumerator GetDescendingEnumerator()
        {
            return new DescendingPeekingEnumerator<AbstractLinkedDeque<T>, T>(this);
        }

        public T Peek()
        {
            return PeekFirst();
        }

        public T PeekFirst()
        {
            return first;
        }

        public T PeekLast()
        {
            return last;
        }

        public bool Offer(T element)
        {
            return OfferLast(element);
        }

        public bool OfferFirst(T element)
        {
            if (Contains(element))
                return false;

            LinkFirst(element);
            return true;
        }

        public bool OfferLast(T element)
        {
            if (Contains(element))
                return false;

            LinkLast(element);
            return true;
        }

        public void Push(T element)
        {
            AddFirst(element);
        }

        public T Pop()
        {
            return UnlinkFirst();
        }
    }
}
