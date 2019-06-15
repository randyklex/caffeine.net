using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

using Caffeine.Cache.Interfaces;
using Caffeine.Cache.MpscQueue;

namespace Caffeine.Tests
{
    public class MpscQueueTests
    {
        public class RefType
        {
            public int someValue;
        }

        #region Testing Constructor Constraints
        [Fact]
        public void GrowableArrayQueueInitialCapacityException()
        {
            Exception e = Assert.Throws<ArgumentOutOfRangeException>(() => new MpscGrowableArrayQueue<int>(1, 1));
        }

        [Fact]
        public void GrowableArrayQueueMaxCapacityException()
        {
            Exception e = Assert.Throws<ArgumentOutOfRangeException>(() => new MpscGrowableArrayQueue<int>(2, 2));
        }

        [Fact]
        public void GrowableArrayQueueInitialLargerThanMaxCapacityException()
        {
            Exception e = Assert.Throws<ArgumentException>(() => new MpscGrowableArrayQueue<int>(6, 4));
        }
        #endregion

        [Fact]
        public void GrowableArrayQueueRefTypeAddRemove()
        {
            IQueue<RefType> queue = new MpscGrowableArrayQueue<RefType>(2, 100);
            queue.Enqueue(new RefType() { someValue = 1 });
            RefType i = queue.Dequeue();

            Assert.Equal<int>(1, i.someValue);
        }

        [Fact]
        public void GrowableArrayQueueRefTypeAddMaxRemoveMax()
        {
            IQueue<RefType> queue = new MpscGrowableArrayQueue<RefType>(2, 4);
            queue.Enqueue(new RefType() { someValue = 1 });
            queue.Enqueue(new RefType() { someValue = 2 });
            queue.Enqueue(new RefType() { someValue = 3 });
            queue.Enqueue(new RefType() { someValue = 4 });
            RefType i = queue.Dequeue();
            RefType i2 = queue.Dequeue();
            RefType i3 = queue.Dequeue();
            RefType i4 = queue.Dequeue();

            Assert.Equal<int>(4, i4.someValue);
        }
    }
}
