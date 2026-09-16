namespace PoolSystem
{
    /// <summary>
    /// Explains why a call to <see cref="IObjectPool{T}.Release"/> was rejected instead of being applied.
    /// </summary>
    public enum InvalidReleaseReason
    {
        /// <summary>
        /// The instance was already released and is currently sitting inactive in the pool.
        /// Releasing it again would push a duplicate reference onto the inactive stack and corrupt
        /// the pool, so the second call is ignored instead.
        /// </summary>
        AlreadyReleased,

        /// <summary>
        /// The instance was never created by this pool (it was not found in the pool's known-instance
        /// set), for example because it came from a different pool or was constructed manually.
        /// </summary>
        NotOwnedByPool
    }
}
