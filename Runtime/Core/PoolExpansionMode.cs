namespace PoolSystem
{
    /// <summary>
    /// Controls what happens when <see cref="IObjectPool{T}.Get"/> is called while the pool has no
    /// inactive instances left to reuse and has already created <c>MaxSize</c> instances.
    /// </summary>
    public enum PoolExpansionMode
    {
        /// <summary>
        /// The pool never creates more than <c>MaxSize</c> instances. Calling <c>Get()</c> at capacity
        /// throws a <see cref="PoolExhaustedException"/> instead of returning a new instance.
        /// Use this for hard resource limits (e.g. a fixed-size particle budget).
        /// </summary>
        Fixed,

        /// <summary>
        /// The pool silently creates a new instance beyond <c>MaxSize</c>, growing without an upper
        /// bound. <c>MaxSize</c> is effectively treated as a soft, informational target only.
        /// Use this when correctness matters more than a strict memory ceiling.
        /// </summary>
        DynamicExpand,

        /// <summary>
        /// Same behavior as <see cref="DynamicExpand"/> (a new instance is created), but the pool
        /// raises its expansion notification every time it grows past <c>MaxSize</c>, so the host
        /// application can log a warning. Use this during development to catch pools that are
        /// undersized without crashing the game.
        /// </summary>
        ExpandAndWarn
    }
}
