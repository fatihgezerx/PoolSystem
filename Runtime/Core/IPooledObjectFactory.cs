namespace PoolSystem
{
    /// <summary>
    /// Abstracts how a pool creates and destroys instances of <typeparamref name="T"/>.
    /// Pooling logic never calls <c>new</c> or <c>UnityEngine.Object.Instantiate</c> directly;
    /// it always goes through a factory. This is what lets <see cref="GenericPool{T}"/> stay
    /// completely ignorant of whether it is pooling plain C# objects, GameObjects, or anything else.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    public interface IPooledObjectFactory<T> where T : class
    {
        /// <summary>Creates a brand new instance. Called only when the pool has no inactive instances to reuse.</summary>
        T Create();

        /// <summary>Permanently destroys an instance. Called when the pool shrinks or is cleared.</summary>
        void Destroy(T item);
    }
}
