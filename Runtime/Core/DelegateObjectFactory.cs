using System;

namespace PoolSystem
{
    /// <summary>
    /// A convenience <see cref="IPooledObjectFactory{T}"/> backed by plain delegates, for when
    /// writing a dedicated factory class would be overkill (tests, simple POCO pools, prototyping).
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    public sealed class DelegateObjectFactory<T> : IPooledObjectFactory<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _destroy;

        /// <summary>
        /// Creates a factory that delegates <see cref="Create"/> and <see cref="Destroy"/> to the supplied functions.
        /// </summary>
        /// <param name="create">Invoked by <see cref="Create"/>. Must not be null.</param>
        /// <param name="destroy">Invoked by <see cref="Destroy"/>. Optional; a no-op is used if null.</param>
        public DelegateObjectFactory(Func<T> create, Action<T> destroy = null)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _destroy = destroy;
        }

        /// <inheritdoc />
        public T Create() => _create();

        /// <inheritdoc />
        public void Destroy(T item) => _destroy?.Invoke(item);
    }
}
