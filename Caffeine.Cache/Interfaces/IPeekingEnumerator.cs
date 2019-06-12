using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Caffeine.Cache.Interfaces
{
    public interface IPeekingEnumerator<T> : IEnumerator<T>, IEnumerator
    {
        /// <summary>
        /// Returns the next element in the iteration without advancing the iteration.
        /// </summary>
        /// <returns></returns>
        T Peek();
    }
}
