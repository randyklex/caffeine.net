This is not yet ready for release.

This is a port of Caffeine originally written for Java by Ben Maines. (https://github.com/ben-manes/caffeine)
This version of Caffeine.NET has been written for .NET Core 2.1 (https://github.com/dotnet/core/releases)

This is still in development. A good bulk of the code has been written, but there remains a lot of debugging and testing to be done. If you're interested in helping let us know. We're looking for committers.

We ommitted Weak References for this version. In Java there is something known as a ReferenceQueue. When a weak reference is collected it's added to that queue. This allows removing the collected weak references by simply iterating that queue. There is no equivalent in .NET., making it problematic for knowing when to remove an item from the cache.

The underlying cache can be accessed as a concurrent dictionary by the function AsConcurrentDictionary() from the ICache interface. The only ConcurrentDictionary functions that have been implemented are
- IsEmpty
- Count
- Clear
- ContainsKey
- TryGetValue
- Remove
- TryRemove

We have not implemented the Loading caches yet nor the Async caches. While some of the code exists there remains work to finish these features.

