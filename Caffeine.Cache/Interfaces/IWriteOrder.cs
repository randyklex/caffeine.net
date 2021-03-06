﻿/*
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
    /// <summary>
    /// An element that is linked on the deque.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IWriteOrder<T>
    {
        /// <summary>
        /// Retrieves the element or <see langword="null"/> if either the element is unlinked or
        /// the first element on the deque.
        /// </summary>
        /// <returns></returns>
        T GetPreviousInWriteOrder();

        /// <summary>
        /// Sets the previous element or <see langword="null"/> if there is no link.
        /// </summary>
        /// <param name="prev"></param>
        void SetPreviousInWriteOrder(T prev);

        /// <summary>
        /// Retrieves the next element or <see langword="null"/> if either the element is unlinked or the
        /// last element on the deque.
        /// </summary>
        /// <returns></returns>
        T GetNextInWriteOrder();

        /// <summary>
        /// Sets the next element or <see langword="null"/> if there is no link.
        /// </summary>
        /// <param name="next"></param>
        void SetNextInWriteOrder(T next);
    }
}
