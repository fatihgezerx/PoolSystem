namespace PoolSystem
{
    /// <summary>
    /// Engine-agnostic contract for an object pool of <typeparamref name="T"/>.
    /// Implementations must guarantee steady-state <see cref="Get"/>/<see cref="Release"/> calls
    /// are free of managed heap allocations once the pool has warmed up.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>Total number of instances currently owned by the pool (active + inactive).</summary>
        int Count { get; }

        /// <summary>Number of instances currently checked out via <see cref="Get"/> and not yet released.</summary>
        int CountActive { get; }

        /// <summary>Number of instances currently sitting idle, available to be returned by <see cref="Get"/>.</summary>
        int CountInactive { get; }

        /// <summary>
        /// Retrieves an instance from the pool, creating a new one if none are available and the
        /// pool's expansion policy allows it.
        /// </summary>
        /// <returns>A ready-to-use instance.</returns>
        T Get();

        /// <summary>
        /// Returns a previously obtained instance to the pool, making it available for reuse.
        /// </summary>
        /// <param name="item">The instance to release. Must have been obtained from this pool via <see cref="Get"/>.</param>
        void Release(T item);

        /// <summary>
        /// Destroys all currently inactive (pooled) instances and empties the pool.
        /// Instances that are still active (checked out) are left untouched and are not tracked further
        /// once released after a <see cref="Clear"/> was called on a pool that is about to be discarded.
        /// </summary>
        void Clear();
    }
}
