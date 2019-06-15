using System;
using System.Collections.Generic;
using System.Text;

namespace Caffeine.Cache.Interfaces
{
    internal interface IQueue<T>
    {
        bool Enqueue(T item);

        T Dequeue();

        T Peek();

        bool IsEmpty { get; }

        int Capacity { get; }

        int Size();
    }
}
