using System;

namespace PoolSystem
{
    /// <summary>
    /// Thrown by <see cref="GenericPool{T}.Get"/> when the pool is configured with
    /// <see cref="PoolExpansionMode.Fixed"/> and has no inactive instances left after
    /// reaching its configured maximum size.
    /// </summary>
    public sealed class PoolExhaustedException : Exception
    {
        /// <summary>The pooled element type that ran out of capacity.</summary>
        public Type PooledType { get; }

        /// <summary>The configured maximum size that was reached.</summary>
        public int MaxSize { get; }

        /// <summary>Creates a new <see cref="PoolExhaustedException"/> for the given pooled type and capacity.</summary>
        public PoolExhaustedException(Type pooledType, int maxSize)
            : base($"Pool of type '{pooledType.Name}' is exhausted: MaxSize ({maxSize}) reached and " +
                   $"expansion mode is {nameof(PoolExpansionMode.Fixed)}. Increase MaxSize, switch to a " +
                   $"different {nameof(PoolExpansionMode)}, or release instances before calling Get() again.")
        {
            PooledType = pooledType;
            MaxSize = maxSize;
        }
    }
}
