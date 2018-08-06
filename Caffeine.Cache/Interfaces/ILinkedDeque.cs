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
    public interface ILinkedDeque<T> : IEnumerable<T>, ICollection<T>
    {
        /// <summary>
        /// Returns if the element is at the front of the deque.
        /// </summary>
        /// <param name="element">The linked element</param>
        /// <returns></returns>
        bool IsFirst(T element);

        /// <summary>
        /// Returns if the elemtn is at the back of the deque.
        /// </summary>
        /// <param name="element">The linked element</param>
        /// <returns></returns>
        bool IsLast(T element);

        /// <summary>
        /// Moves the element to the front of the dequeu so that it becomes the first element.
        /// </summary>
        /// <param name="element">The linked element</param>
        void MoveToFront(T element);

        /// <summary>
        /// Moves the element to the bak of the deque so that it becomes the last element.
        /// </summary>
        /// <param name="element">The linked element</param>
        void MoveToBack(T element);

        /// <summary>
        /// Retrieves the previous element or NULL if either the element is unlinked
        /// or the first element on the deque.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        T GetPrevious(T element);

        /// <summary>
        /// Sets the previous element or null if there is no link.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="previousElement"></param>
        void SetPrevious(T element, T previousElement);

        /// <summary>
        /// Retrieves the next element or NULL if either element is unlinked or the last
        /// element ont he deque.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        T GetNext(T element);

        /// <summary>
        /// Sets the next element or NULL if there is no link.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="nextElement"></param>
        void SetNext(T element, T nextElement);

        /// <summary>
        /// Gets the first node of the LinkedList
        /// </summary>
        T First { get; }

        /// <summary>
        /// Gets the last node of the LinkedList
        /// </summary>
        T Last { get; }

        /// <summary>
        /// Gets the number of nodes actually contained in the LinkedList
        /// </summary>
        new int Count { get; }

    }
}
