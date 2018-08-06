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
using System.Text;

using Caffeine.Cache.Interfaces;
using Caffeine.Cache.Stats;

namespace Caffeine.Cache
{
    public sealed class Caffeine<K, V>
    {
        public enum Strength
        {
            UNSET,
            STRONG,
            WEAK,
        }

        private const int UNSET_INT = -1;

        private const int DEFAULT_INITIAL_CAPACITY = 16;
        private const int DEFAULT_EXPIRATION_NANOSECONDS = 0;
        private const int DEFAULT_REFRESH_NANOSECONDS = 0;

        private bool strictParsing = true;

        private long maximumSize = UNSET_INT;
        private long maximumWeight = UNSET_INT;
        private int initialCapacity = UNSET_INT;

        private long refreshNanoseconds = UNSET_INT;
        private long expireAfterWriteNanoseconds = UNSET_INT;
        private long expireAfterAccessNanoseconds = UNSET_INT;

        private IRemovalListener<K, V> removalListener;
        private IStatsCounterSupplier<IStatsCounter> statsCounterSupplier;
        private ICacheWriter<K, V> writer;
        private IWeigher<K, V> weigher;
        private IExpiry<K, V> expiry;
        private ITicker ticker;

        private bool isAsync;

        private readonly IStatsCounterSupplier<IStatsCounter> ENABLED_STATS_COUNTER_SUPPLIER = new ConcurrentStatsCounterSupplier();

        private Caffeine()
        {
            KeyStrength = Strength.STRONG;
            ValueStrength = Strength.STRONG;
        }

        /// <summary>
        /// Constructs a new <see cref="Caffeine{K, V}"/> instance with default settings, including strong keys,
        /// strong values and no automatic eviction of any kind.
        /// </summary>
        /// <returns></returns>
        public static Caffeine<K, V> Builder()
        {
            return new Caffeine<K, V>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Caffeine<K, V> MakeAsync()
        {
            isAsync = true;
            return this;
        }

        public bool IsAsync
        {
            get { return isAsync; }
            set { isAsync = value; }
        }

        /// <summary>
        /// The minimum total size for the internal data structures. Providing a large
        /// enough estimate at construction time avoids the need for expensive resizing
        /// operations later, but setting this value unnecessarily high wastes memory.
        /// </summary>
        public int InitialCapacity
        {
            get { return HasInitialCapacity ? initialCapacity : DEFAULT_INITIAL_CAPACITY; }
        }

        public Caffeine<K, V> ConfigureInitialCapacity(int initialCapacity)
        {
            if (this.initialCapacity != UNSET_INT)
                throw new InvalidOperationException("initial capacity was already set to " + this.initialCapacity.ToString());

            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException("value", "initial capacity has to be greater than 0.");

            this.initialCapacity = initialCapacity;

            return this;
        }

        public bool HasInitialCapacity
        {
            get { return initialCapacity != UNSET_INT; }
        }

        /// <summary>
        /// Maximum number of entries the cache may contain. Note that the cache may
        /// evict an entry before this limit is exceed or temporarily exceed the threshold
        /// while evicting. As the cache size grows close to the maximum, the cache evicts
        /// entries that are less likely to be used again. For example, the cache may evict
        /// an entry because it hasn't been used recently or very often.
        /// 
        /// When size is zero, elements will be evicted immediately after being loaded
        /// into the cache. This can be useful in testing, or to disable caching temporarily
        /// without a code change.
        /// 
        /// This feature cannot be used in conjunction with <see cref="MaximumWeight"/>.
        /// </summary>
        public long MaximumSize
        {
            get { return maximumSize; }
        }

        public Caffeine<K, V> SpecifyMaximumSize(long maximumSize)
        {
            if (this.maximumSize != UNSET_INT)
                throw new InvalidOperationException("maximum size was already set to " + this.maximumSize.ToString());

            if (this.maximumWeight != UNSET_INT)
                throw new InvalidOperationException("maximum weight was already set to " + this.maximumWeight.ToString());

            if (this.weigher != null)
                throw new InvalidOperationException("maximum size cannot be combined with a weigher.");

            // TODO: this value in the original code would allow 0 max size.. but that seems to be an inappropriate range
            if (maximumSize <= 0)
                throw new ArgumentOutOfRangeException("maximumSize", "maximum size must be greater than 0.");

            this.maximumSize = maximumSize;

            return this;
        }

        /// <summary>
        /// Specifies the maximum weight of entries in the cache may contain. Weight is determined using the Weigher
        /// specified with Weigher, and use of this method requires a corresponding call to Weigher
        /// prior to calling build.
        /// 
        /// Note that the cache may evict an entry before this limit is exceeded or temporarily exceed
        /// the threshold while evicting. As the cache size grows close to the maximum, the cache evicts
        /// entries that are less likely to be used again. For example, the cache may evict an entry
        /// because it hasn't been used recently or very often.
        /// 
        /// When maximumWeight is zero, elements will be evicted immediately after being loaded
        /// into the cache. This can be useful in testing, or to disable caching temporarily without
        /// a code change.
        /// 
        /// Note that weight is only used to determine whether the cache is over capacity; it has no effect
        /// on selecting which entry should be evicted next.
        /// 
        /// This fteature cannot be used in conjunction with <see cref="MaximumSize"/>
        /// </summary>
        public long MaximumWeight
        {
            get { return maximumWeight; }
        }

        public Caffeine<K, V> SpecifyMaximumWeight(long maximumWeight)
        {
            if (this.maximumWeight != UNSET_INT)
                throw new InvalidOperationException("maximum weight was already set to " + this.maximumWeight.ToString());

            if (this.maximumSize != UNSET_INT)
                throw new InvalidOperationException("maximum size was already set to " + this.maximumSize.ToString());

            // TODO: this value in the original code would allow 0 max weight.. is that OK?
            if (maximumWeight <= 0)
                throw new ArgumentOutOfRangeException("maximumWeight", "maximum weight must be greater than 0.");

            this.maximumWeight = maximumWeight;

            return this;
        }

        /// <summary>
        /// Specifies the weigher to use in determining the weight of entries. Entry weight
        /// is taken into considuration by <see cref="MaximumWeight"/> when determining which entries to
        /// evict, and use of this method requires a corresponding call to <see cref="MaximumWeight"/> prior
        /// to calling Build. Weights are measured and recorded when entries are inserted into
        /// or updated in the cache, and thus are effectively static during the lifetime of
        /// a cache entry.
        /// 
        /// When the weight of an entry is zero it will not be considered for size-based
        /// eviction (though it still may be evicted by other means).
        /// 
        /// Important Note: instead of returning <see cref="this"/> as a <see cref="Caffeine{K, V}"/> instance,
        /// this method returns <see cref="Caffeine{K, V}"/>. From this point on, either the original reference
        /// or the returned reference may be used to complete configuration and build the cache, but only the
        /// "generic" one is type-safe. That is, it will properly prevent you from building caches whose
        /// key or value types are incompatible with the types accepted by the weigher already provided.
        /// The <see cref="Caffeine{K, V}"/> type cannot do this. For best results, simply use the standard
        /// method-chaining idiom, as illustrated in the documentation at top, configuring
        /// 
        /// Warning: if you ignore the above advice, and use this to build a cache
        /// whose key or value type is incompatible with the weigher, you will likely
        /// exerpeience a Cast Exception at some undeinfed point in the future.
        /// </summary>
        public IWeigher<K, V> Weigher
        {
            get
            {
                IWeigher<K, V> @delegate = (weigher == null) || weigher is SingletonWeigher<K, V> ? SingletonWeigher<K, V>.Instance : new BoundedWeigher<K, V>(weigher);

                return isAsync ? (IWeigher<K, V>)new AsyncWeigher<K, V>(@delegate) : @delegate;
            }
        }

        public Caffeine<K, V> SpecifyWeigher(IWeigher<K, V> weigher)
        {
            if (weigher == null)
                throw new ArgumentNullException("weigher", "weigher cannot be null.");

            if (this.weigher != null)
                throw new InvalidOperationException("weigher was already set.");

            if (strictParsing && maximumSize != UNSET_INT)
                throw new InvalidOperationException("weigher cannot be combined with maximum size.");

            this.weigher = weigher;

            return this;
        }

        internal bool Evicts
        {
            get { return Maximum != UNSET_INT; }
        }

        internal bool IsWeighted
        {
            get { return weigher != null; }
        }

        internal long Maximum
        {
            get { return IsWeighted ? maximumWeight : maximumSize; }
        }

        /// <summary>
        /// Specifies that each key (not value) stored in the cache should be wrapped in a
        /// <see cref="WeakReference{T}"/> (by default, strong references are used).
        /// 
        /// Warning: When this method is used, the resulting cache will use identity comparison
        /// to determine equality of keys. Its <see cref="ICache{K, V}.AsConcurrentDictionary"/> view
        /// will therefore technically violate the <see cref="ConcurrentDictionary"/> specification
        /// (in the same way that IdentityHashMap does.
        /// 
        /// Entries with keys that have been garbage collected may be counted in <see cref="ICache{K, V}.EstimatedSize"/>,
        /// but will never be visible to read or write operations; such entries are cleaned up as part
        /// of the routine maintenance.
        /// 
        /// This feature cannot be used in conjunction with <see cref="Writer"/>
        /// </summary>
        /// <returns></returns>
        public Caffeine<K, V> SpecifyWeakKeys()
        {
            KeyStrength = Strength.WEAK;
            return this;
        }

        public Strength KeyStrength { get; set; }

        public bool IsStrongKeys {  get { return KeyStrength == Strength.STRONG; } }

        /// <summary>
        /// Specifies that each value (not key) stored in the cache should be wrapped in a 
        /// <see cref="WeakReference{T}"/> (by default, strong references are used).
        /// 
        /// Weak values will be garbage collected once they are weakly reachable. This makes them a
        /// poor candidate for caching; consider <see cref="SoftValues"/> instead.
        /// 
        /// NOTE: When this method is used, the resulting cache will use identity comparison
        /// to determin equality of values.
        /// 
        /// Entries with values that have been garbage collected may be counted in <see cref="ICache{K, V}.EstimatedSize"/>,
        /// but will never be visible to read or write operations; such entries are cleaned up as part of the routine
        /// maintenance.
        /// 
        /// This feature cannot be used in conjunction with <see cref="BuildAsync()"/>
        /// </summary>
        /// <returns></returns>
        public Caffeine<K, V> SpecifyWeakValues()
        {
            ValueStrength = Strength.WEAK;
            return this;
        }

        public Strength ValueStrength { get; set; }

        public bool IsStrongValues {  get { return ValueStrength == Strength.STRONG; } }

        public bool IsWeakValues {  get { return ValueStrength == Strength.WEAK; } }

        /// <summary>
        /// Specifies that each entry should be automatically removed from the cache once a fixed
        /// duration has elapsed after the entry's creation, or the most recent replacement of its value.
        /// 
        /// Expired entries may be counted in <see cref="ICache{K, V}.EstimatedSize"/>, but will never be
        /// visible to read or write operations. Expired entries are cleaned up as part of 
        /// routine maintenance.
        /// </summary>
        /// <param name="duration">The length of time after an entry is created that it should be automatically removed.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="duration"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">If the time to live, or the variable expiration was already set.</exception>
        public Caffeine<K, V> ExpireAfterWrite(TimeSpan duration)
        {
            // From MSDN docs, a tick is a 100-nanosecond unit.
            ExpireAfterWriteNanoseconds = duration.Ticks * 100;

            return this;
        }

        public long ExpireAfterWriteNanoseconds
        {
            get { return DoesExpireAfterWrite ? expireAfterWriteNanoseconds : DEFAULT_EXPIRATION_NANOSECONDS; }
            set
            {
                if (expireAfterWriteNanoseconds != UNSET_INT)
                    throw new InvalidOperationException("ExpireAfterWrite was already set to " + expireAfterWriteNanoseconds.ToString());

                if (expiry != null)
                    throw new InvalidOperationException("ExpireAfterWrite may not be used with variable expiration.");

                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "ExpireAfterWrite duration cannot be less than zero.");

                expireAfterWriteNanoseconds = value;
            }
        }

        public bool DoesExpireAfterWrite
        {
             get { return expireAfterWriteNanoseconds != UNSET_INT; }
        }

        /// <summary>
        /// Specifies taht each entry should be automatically removed from the cache once a 
        /// fixed duration has elapsed after the entry's creation, the most replacement of its
        /// value, or its last access. Access time is reset by all cache read and write operations
        /// (including <see cref="ICache{K, V}.TryGetValue(K)"/> and <see cref="ICache{K, V}.Add(K, V)"/>,
        /// but not by operations on the collection-views of 
        /// 
        /// Expired entries may be counted in <see cref="ICache{K, V}.EstimatedSize"/>, but will never
        /// be visible to read or write operations. Expired entries are cleaned up as part of the
        /// routine maintenance.
        /// </summary>
        /// <param name="duration">The lenght of time after an entry is last accessed that it should be automatically removed.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="duration"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">If the expire after access time was already set.</exception>
        public Caffeine<K, V> ExpireAfterAccess(TimeSpan duration)
        {
            // From MSDN docs, a tick is a 100-nanosecond unit.
            ExpireAfterAccessNanoseconds = duration.Ticks * 100;
            return this;
        }

        public long ExpireAfterAccessNanoseconds
        {
            // TODO: there are a couple properties that are always comparing as to whether it's UNSET. can we construct one whos values are set to the default and avoid these comparisons?
            get { return DoesExpireAfterAccess ? expireAfterAccessNanoseconds : DEFAULT_EXPIRATION_NANOSECONDS; }
            set
            {
                if (expireAfterAccessNanoseconds != UNSET_INT)
                    throw new InvalidOperationException("ExpireAfterAccess was already set to " + expireAfterAccessNanoseconds.ToString());

                if (expiry != null)
                    throw new InvalidOperationException("ExpireAfterAccess may not be used with variable expiration.");

                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "ExpireAfterAccess cannot be less than zero.");

                expireAfterAccessNanoseconds = value;
            }
        }

        public bool DoesExpireAfterAccess
        {
             get { return expireAfterAccessNanoseconds != UNSET_INT; }
        }

        /// <summary>
        /// Specifies that each entry should be automatically removed from the cache once a duration
        /// has elapsed after the entry's creation, the most recent replacement of its value, or its
        /// last read. The expiration time is reset by all cache read and write operations (including
        /// <see cref="ICache{K, V}.TryGetValue(K)"/> and <see cref="ICache{K, V}.Add(K, V)"/>.
        /// 
        /// Expired entries may be counted in <see cref="ICache{K, V}.EstimatedSize"/>, but will never be
        /// visible to read or write operations. Expired entries are cleaned up as part of the routine
        /// maintenance.
        /// </summary>
        /// <param name="expiry">The expiry to use in calculating the expiration time of cache entries</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">if expiration was already set.</exception>
        /// <exception cref="ArgumentNullException">if expiry is <see langword="null"/></exception>
        public Caffeine<K, V> SpecifyExpireVariable(IExpiry<K, V> expiry)
        {
            if (expireAfterAccessNanoseconds != UNSET_INT)
                throw new InvalidOperationException("Expirer may not be used with ExpireAfterAccess.");

            if (expireAfterWriteNanoseconds != UNSET_INT)
                throw new InvalidOperationException("Expirer may not be used with ExpireAfterWrite.");

            if (this.expiry != null)
                throw new InvalidOperationException("Expirer was already set.");

            if (expiry == null)
                throw new ArgumentNullException("expiry", "Expirer cannot be set to NULL.");


            this.expiry = expiry;
            return this;
        }

        public IExpiry<K, V> ExpireVariable
        {
            get
            {
                if (IsAsync && expiry != null)
                    expiry = (IExpiry<K, V>)(new AsyncExpiry<K, V>(expiry));

                return expiry;
            }
        }

        public bool DoesExpireVariable
        {
            get { return (expiry != null); }
        }

        /// <summary>
        /// Specifies that active entries are elgibile for automatic refresh once a fixed duration has
        /// elapsed after the entry's creation, or the most recent replacement of its value. The semantics
        /// of refreshes are speicifed in <see cref="ILoadingCache{K, V}.Refresh(K)"/>, and are performed
        /// by calling <see cref="ICacheLo"/>
        /// 
        /// Automatic refreshes are performed when the first stale request for an entry occurs. The request
        /// triggering refresh will make an asynchronous call to <see cref="CacheLoader{K, V}.Reload(K, V)"/>
        /// and immediately return the old value.
        /// 
        /// NOTE: All exceptions thrown during refresh will be logged and then swallowed.
        /// </summary>
        /// <param name="duration">Duration in nanoseconds</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">if the refresh interval was already set.</exception>
        /// <exception cref="ArgumentOutOfRangeException">the <paramref name="durationNanos"/> is zero or negative.</exception>
        public Caffeine<K, V> RefreshAfterWrite(TimeSpan duration)
        {
            // From MSDN docs, a tick is a 100-nanosecond unit.
            RefreshAfterWriteNanoseconds = duration.Ticks * 100;
            return this;
        }

        public long RefreshAfterWriteNanoseconds
        {
            get { return refreshNanoseconds != UNSET_INT ? refreshNanoseconds : DEFAULT_REFRESH_NANOSECONDS; }
            set
            {
                if (refreshNanoseconds != UNSET_INT)
                    throw new InvalidOperationException("RefreshAfterWrite was already set to " + refreshNanoseconds.ToString());

                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value", "Duration must be gretaer than 0");

                refreshNanoseconds = value;
            }
        }

        public bool DoesRefreshAfterWrite
        {
            get { return refreshNanoseconds != UNSET_INT; }
        }

        /// <summary>
        /// Specifies a nanosecond-precision time source for us in determining when entries should
        /// be expired or refreshed. By deafult, <see cref="SystemTicker"/> is used.
        /// </summary>
        /// <param name="ticker">A nano-second precision time source.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If the <see cref="Ticker"/> was already set.</exception>
        /// <exception cref="ArgumentNullException">If the ticker is <see langword="null"/></exception>
        public Caffeine<K, V> SpecifyTicker(ITicker ticker)
        {
            Ticker = ticker;
            return this;
        }

        public ITicker Ticker
        {
            get
            {
                // TODO: original code has a "IsRecordingStats".. add the stats stuff..
                bool useTicker = (DoesExpireAfterWrite || DoesExpireAfterAccess || DoesExpireVariable || DoesRefreshAfterWrite || IsRecordingStats);

                ITicker rval = null;
                if (useTicker)
                {
                    if (ticker == null)
                        rval = SystemTicker.Instance;
                    else
                        rval = ticker;
                }
                else
                    rval = DisabledTicker.Instance;

                return rval;
            }
            set
            {
                if (ticker != null)
                    throw new InvalidOperationException("Ticker was already set.");

                ticker = value ?? throw new ArgumentNullException("value", "Ticker cannot be NULL.");
            }
        }

        /// <summary>
        /// Specifies a listener instance that caches should notify each time an entry is removed for any
        /// <see cref="RemovalCause"/> reason. Each cache created by this builder will invoke this listener
        /// as part of the routine maintenance describe in the class documentation above.
        /// </summary>
        /// <param name="removalListener">A listener instance that caches should notify each time an entry is removed.</param>
        /// <exception cref="InvalidOperationException">If a removal listener was already set.</exception>
        /// <exception cref="ArgumentNullException">If the <paramref name="removalListener"/> is <see langword="null"/></exception>
        public Caffeine<K, V> SpecifyRemovalListener(IRemovalListener<K, V> removalListener)
        {
            RemovalListener = removalListener;
            return this;
        }

        /// <summary>
        /// Specifies a listener instance that caches should notify each time an entry is removed for any
        /// <see cref="RemovalCause"/> reason. Each cache created by this builder will invoke this listener
        /// as part of the routine maintenance describe in the class documentation above.
        /// </summary>
        public IRemovalListener<K, V> RemovalListener
        {
            get
            {
                return (isAsync && removalListener != null) ? new AsyncRemovalListener<K, V>(removalListener) : removalListener;
            }
            set
            {
                if (removalListener != null)
                    throw new InvalidOperationException("RemovalListener was already set.");

                removalListener = value ?? throw new ArgumentNullException("value", "RemovalListener cannot be NULL.");
            }
        }

        /// <summary>
        /// Specifies a writer instance that caches should notify each time an entry is explicitly created
        /// or modified, or removed for any <see cref="RemovalCause"/> reason. The writer is not notified
        /// when an entry is loaded or computed. Each cache created by this builder will invoke this
        /// writer as part of the atomic operation that modifies the cache.
        /// </summary>
        /// <param name="writer"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">If a writer was already set or if the <see cref="KeyStrength"/> is weak</exception>
        /// <exception cref="ArgumentNullException">If the writer is <see langword="null"/></exception>
        public Caffeine<K, V> SpecifyWriter(ICacheWriter<K, V> writer)
        {
            Writer = writer;
            return this;
        }

        public ICacheWriter<K, V> Writer
        {
            get
            {
                return (writer == null) ? (ICacheWriter<K, V>)DisabledCacheWriter<K, V>.Instance : writer;
            }
            set
            {
                if (writer != null)
                    throw new InvalidOperationException("Writer was already set.");

                if (!IsStrongKeys)
                    throw new InvalidOperationException("Weak keys may not be used with CacheWriter.");

                writer = value ?? throw new ArgumentNullException("Writer cannot be NULL.");
            }
        }

        /// <summary>
        /// Enables the accumulation of <see cref="CacheStats"/> during the operation of the cache. Without this
        /// <see cref="ICache{K, V}.Stats"/> will return zero for all statistics. Note that recording statistics
        /// requires bookkeeping to be performed with each operation, and thus imposes a performance penalty
        /// on each cache operation.
        /// </summary>
        /// <returns></returns>
        public Caffeine<K, V> RecordStats()
        {
            StatsCounter = ENABLED_STATS_COUNTER_SUPPLIER;
            return this;
        }

        /// <summary>
        /// Enables the accumulation of <see cref="CacheStats"/> during the operation of the cache. Without this
        /// <see cref="ICache{K, V}.Stats"/> will return zero for all statistics. Note that recording statistics
        /// requires bookkeeping to be performed with each operation, and thus imposes a performance penalty
        /// on each cache operation.
        /// </summary>
        /// <param name="statsCounterSupplier">A supplier that returns a new <see cref="IStatsCounter"/></param>
        /// <returns></returns>
        public Caffeine<K, V> RecordStats<T>(IStatsCounterSupplier<T> statsCounterSupplier) where T : IStatsCounter
        {
            StatsCounter = new GuardedStatsCounterSupplier<IStatsCounter>((IStatsCounterSupplier<IStatsCounter>)statsCounterSupplier);
            return this;
        }

        public IStatsCounterSupplier<IStatsCounter> StatsCounter
        {
            get
            {
                return statsCounterSupplier == null ? (IStatsCounterSupplier<IStatsCounter>)DisabledStatsCounter.Instance : statsCounterSupplier;
            }
            set
            {
                if (statsCounterSupplier != null)
                    throw new InvalidOperationException("Statistics recording was already set.");

                if (value == null)
                    throw new ArgumentNullException("statistics counter is a required parameter.");

                statsCounterSupplier = value;
            }
        }

        public bool IsRecordingStats
        {
            get { return statsCounterSupplier != null; }
        }

        public bool IsBounded
        {
            get {
                return (maximumSize != UNSET_INT)
                 || (maximumWeight != UNSET_INT)
                 || (expireAfterAccessNanoseconds != UNSET_INT)
                 || (expireAfterWriteNanoseconds != UNSET_INT)
                 || (expiry != null)
                 || (KeyStrength != Strength.UNSET)
                 || (ValueStrength != Strength.UNSET);
            }
        }



        /// <summary>
        /// Builds a cache which does not automatically load values when keys are requested.
        /// </summary>
        /// <typeparam name="K1"></typeparam>
        /// <typeparam name="V1"></typeparam>
        /// <returns></returns>
        public ICache<K, V> Build()
        {
            RequireWeightWithWeigher();
            RequireNonLoadingCache();

            return (IsBounded || DoesRefreshAfterWrite) ? (ICache<K, V>)new BoundedManualCache<K, V>(this) : (ICache<K, V>)new UnboundedManualCache<K, V>(this);
        }

        /// <summary>
        /// Builds a cache which does not automatically load values when keys are requested.
        /// This method does not alter the state of this <see cref="Caffeine{K, V}"/> instance,
        /// so it can be invoked again to create multiple independent caches.
        /// </summary>
        /// <typeparam name="K1"></typeparam>
        /// <typeparam name="V1"></typeparam>
        /// <returns></returns>
        public ILoadingCache<K, V> Build(CacheLoader<K, V> loader)
        {
            RequireWeightWithWeigher();

            // TODO: implement the other type of loading cache.. 
            //return (IsBounded || DoesRefreshAfterWrite) ? (ILoadingCache<K, V>)new BoundedLoadingCache<K, V>(this, loader) : (ILoadingCache<K, V>)new UnboundedLoadingCache<K, V>(this, loader);
            return (ILoadingCache<K, V>)new BoundedLoadingCache<K, V>(this, loader);
        }

        public IAsyncLoadingCache<K, V> BuildAsync(CacheLoader<K, V> loader)
        {
            return BuildAsync((AsyncCacheLoader<K, V>)loader);
        }

        public IAsyncLoadingCache<K, V> BuildAsync(AsyncCacheLoader<K, V> loader)
        {
            RequireWeightWithWeigher();

            if (IsWeakValues)
                throw new InvalidOperationException("Weak values cannot be combined with AsyncLoadingCache.");

            if (loader == null)
                throw new ArgumentNullException("loader", "loader cannot be null.");

            // TODO: implement this..
            //return (IsBounded || DoesRefreshAfterWrite) ? new BoundAsyncLoadingCache<K, V>(this, loader) : new UnboundedAsyncLoadingCache<K, V>(this, loader);
            throw new NotImplementedException();
        }

        private void RequireNonLoadingCache()
        {
            if (refreshNanoseconds != UNSET_INT)
                throw new ArgumentException("RefreshAfterWrite requires a loading cache.");
        }

        private void RequireWeightWithWeigher()
        {
            if (weigher == null && maximumWeight != UNSET_INT)
                throw new InvalidOperationException("MaximumWeight requires a weigher.");

            if (strictParsing && weigher != null && maximumWeight == UNSET_INT)
                throw new InvalidOperationException("Weigher requires a MaximumWeight.");

            // TODO: log warning that weigher is SET, but maximumWeight HAS NOT BEEN set.

        }

        /// <summary>
        /// Returns a string representation for this <see cref="Caffeine{K, V}"/> instance. The exact
        /// form of the returned string is not specified.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(64);
            sb.Append(this.GetType().Name).Append('{');

            int baseLength = sb.Length;

            if (initialCapacity != UNSET_INT)
                sb.Append("initialCapacity=").Append(initialCapacity).Append(", ");

            if (maximumSize != UNSET_INT)
                sb.Append("maximumSize=").Append(maximumSize).Append(", ");

            if (maximumWeight != UNSET_INT)
                sb.Append("maximumWeight=").Append(maximumWeight).Append(", ");

            if (expireAfterWriteNanoseconds != UNSET_INT)
                sb.Append("expireAfterWrite=").Append(expireAfterWriteNanoseconds).Append("ns, ");

            if (expireAfterAccessNanoseconds != UNSET_INT)
                sb.Append("expireAfterAccess=").Append(expireAfterAccessNanoseconds).Append("ns, ");

            if (expiry != null)
                sb.Append("expiry, ");

            if (refreshNanoseconds != UNSET_INT)
                sb.Append("refreshNanos=").Append(refreshNanoseconds).Append("ns, ");

            sb.Append("keyStrength=").Append(KeyStrength.ToString()).Append(", ");
            sb.Append("valueStrength=").Append(ValueStrength.ToString()).Append(", ");

            if (removalListener != null)
                sb.Append("removalListener, ");

            if (writer != null)
                sb.Append("writer, ");

            sb.Append('}');
            return sb.ToString();

        }
    }
}
