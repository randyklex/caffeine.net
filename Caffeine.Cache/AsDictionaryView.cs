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
using System.Threading.Tasks;

namespace Caffeine.Cache
{
    class AsDictionaryView<K, V> : IDictionary<K, V>
    {
        private LocalCache<K, Task<V>> mDelegate;
        private ICollection<V> mValues;

        AsDictionaryView(LocalCache<K, Task<V>> @delegate)
        {
            mDelegate = @delegate;
        }

        public V this[K key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ICollection<K> Keys
        {
            get
            {
                return mDelegate.Keys;
            }
        }

        public ICollection<V> Values
        {
            get
            {
                if(mValues == null)
                {
                    mValues = new ValuesCollection(this);
                }

                return mValues;
            }
        }

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(K key, V value)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (value == null)
                throw new ArgumentNullException("value");

            mDelegate.Add(key, Task.Run(() => { return value; }), true);
        }

        public void Add(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
            // TODO: implement this method
            //mDelegate.keys
            //Add(item.Key, item.Value);
        }

        public void Clear()
        {
            mDelegate.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            var value = mDelegate.TryGetValue(item.Key, true);
            return value.IsCanceled == false && item.Value.Equals(value.Result);
        }

        public bool ContainsKey(K key)
        {
            return mDelegate.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return new KeyValuePairEnumberator(mDelegate);
        }

        public bool Remove(K key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return mDelegate.Remove(key, out Task<V> val);
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            if (item.Key == null)
                throw new ArgumentNullException("item.Key");

            bool removed = false;
            bool done = false;

            if (mDelegate.TryGetValue(item.Key, out Task<V> val))
            {
                V res = val.Result;
                if (res.Equals(item.Value) == false)
                    return false;

                mDelegate.Compute(item.Key, (key, oldvalueTask) =>
                {
                    if (val.Equals(oldvalueTask) == false)
                        return oldvalueTask;

                    done = true;
                    removed = item.Value.Equals(res);
                    return removed ? null : oldvalueTask;
                }, false, false);
            }

            return removed;
        }

        public bool TryGetValue(K key, out V value)
        {
            bool res = mDelegate.TryGetValue(key, out Task<V> val);

            value = res ? val.Result : default(V);

            return res;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return mDelegate.GetEnumerator();
        }

        private sealed class ValuesCollection : ICollection<V>, ICollection, IReadOnlyCollection<V>
        {
            AsDictionaryView<K, V> mDictionary;

            public ValuesCollection(AsDictionaryView<K, V> dictionary)
            {
                if (dictionary == null)
                    throw new ArgumentNullException("dictionary");

                mDictionary = dictionary;
            }

            public int Count
            {
                get
                {
                    return mDictionary.Count;
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return true;
                }
            }

            public bool IsSynchronized
            {
                get
                {
                    return false;
                }
            }

            public object SyncRoot
            {
                get
                {
                    return ((ICollection)mDictionary).SyncRoot;
                }
            }

            public void Add(V item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(V item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(V[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<V> GetEnumerator()
            {
                return new ValueCollectionEnumerator(mDictionary.mDelegate);
            }

            public bool Remove(V item)
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class ValueCollectionEnumerator : IEnumerator<V>, IEnumerator
        {
            private IEnumerator<KeyValuePair<K, Task<V>>> mEnumerator;
            public ValueCollectionEnumerator(IEnumerable<KeyValuePair<K, Task<V>>> enumerable)
            {
                mEnumerator = enumerable.GetEnumerator();
            }

            public V Current
            {
                get
                {
                    return mEnumerator.Current.Value.Result;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }

            public void Dispose()
            {
                mEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                return mEnumerator.MoveNext();
            }

            public void Reset()
            {
                mEnumerator.Reset();
            }
        }

        private sealed class KeyValuePairEnumberator : IEnumerator<KeyValuePair<K, V>>, IEnumerator
        {
            private IEnumerator<KeyValuePair<K, Task<V>>> mEnumerator;
            public KeyValuePairEnumberator(IEnumerable<KeyValuePair<K, Task<V>>> enumerable)
            {
                mEnumerator = enumerable.GetEnumerator();
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }

            public KeyValuePair<K, V> Current
            {
                get
                {
                    return new KeyValuePair<K, V>(mEnumerator.Current.Key, mEnumerator.Current.Value.Result);
                }
            }

            public void Dispose()
            {
                mEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                return mEnumerator.MoveNext();
            }

            public void Reset()
            {
                mEnumerator.Reset();
            }
        }
    }
}
