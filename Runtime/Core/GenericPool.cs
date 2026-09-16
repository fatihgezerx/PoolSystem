using System;
using System.Collections.Generic;

namespace PoolSystem
{
    /// <summary>
    /// A pure C# object pool for any reference type, with no dependency on UnityEngine.
    /// Backed by a <see cref="Stack{T}"/> for O(1) allocation-free <see cref="Get"/>/<see cref="Release"/>
    /// once the pool has warmed up, plus a bookkeeping <see cref="HashSet{T}"/> pair used purely to
    /// detect double-releases and foreign objects (see <see cref="InvalidReleaseDetected"/>).
    /// </summary>
    /// <remarks>
    /// This is the engine-agnostic core of the package. <c>GameObjectPool</c> in the Unity adapter
    /// layer is built on top of this class rather than duplicating pooling logic.
    /// </remarks>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    public class GenericPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _activeSet;
        private readonly HashSet<T> _knownInstances;
        private readonly IPooledObjectFactory<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxSize;
        private readonly PoolExpansionMode _expansionMode;
        private int _totalCreated;

        /// <inheritdoc />
        public int Count => CountActive + CountInactive;

        /// <inheritdoc />
        public int CountActive => _activeSet.Count;

        /// <inheritdoc />
        public int CountInactive => _inactive.Count;

        /// <summary>The configured hard/soft capacity, depending on <see cref="PoolExpansionMode"/>.</summary>
        public int MaxSize => _maxSize;

        /// <summary>The configured behavior for growing past <see cref="MaxSize"/>.</summary>
        public PoolExpansionMode ExpansionMode => _expansionMode;

        /// <summary>
        /// Raised when <see cref="Release"/> is called with an instance that cannot be released,
        /// either because it was already released (<see cref="InvalidReleaseReason.AlreadyReleased"/>)
        /// or because it does not belong to this pool (<see cref="InvalidReleaseReason.NotOwnedByPool"/>).
        /// The offending call is ignored rather than throwing, so callers can subscribe to log a warning
        /// without every misuse crashing the game.
        /// </summary>
        public event Action<T, InvalidReleaseReason> InvalidReleaseDetected;

        /// <summary>
        /// Raised when the pool creates an instance beyond <see cref="MaxSize"/> while running in
        /// <see cref="PoolExpansionMode.ExpandAndWarn"/>. The argument is the new total instance count.
        /// </summary>
        public event Action<int> PoolExpanded;

        /// <summary>
        /// Creates a new <see cref="GenericPool{T}"/>.
        /// </summary>
        /// <param name="factory">Creates and destroys instances of <typeparamref name="T"/>.</param>
        /// <param name="prewarmCount">Number of instances to eagerly create up front.</param>
        /// <param name="maxSize">
        /// The capacity threshold. Its exact meaning depends on <paramref name="expansionMode"/>:
        /// a hard ceiling for <see cref="PoolExpansionMode.Fixed"/>, a soft/informational target otherwise.
        /// </param>
        /// <param name="expansionMode">What to do when <paramref name="maxSize"/> is reached.</param>
        /// <param name="onGet">Optional callback invoked every time an instance is handed out.</param>
        /// <param name="onRelease">Optional callback invoked every time an instance is returned.</param>
        public GenericPool(
            IPooledObjectFactory<T> factory,
            int prewarmCount = 0,
            int maxSize = int.MaxValue,
            PoolExpansionMode expansionMode = PoolExpansionMode.DynamicExpand,
            Action<T> onGet = null,
            Action<T> onRelease = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "MaxSize must be greater than zero.");
            if (prewarmCount < 0) throw new ArgumentOutOfRangeException(nameof(prewarmCount), "PrewarmCount cannot be negative.");

            _factory = factory;
            _maxSize = maxSize;
            _expansionMode = expansionMode;
            _onGet = onGet;
            _onRelease = onRelease;

            var initialCapacity = prewarmCount > 0 ? prewarmCount : 4;
            _inactive = new Stack<T>(initialCapacity);
            _activeSet = new HashSet<T>();
            _knownInstances = new HashSet<T>();

            Prewarm(prewarmCount);
        }

        private void Prewarm(int count)
        {
            var target = Math.Min(count, _maxSize);
            for (var i = 0; i < target; i++)
            {
                var instance = CreateInstance();
                _inactive.Push(instance);
            }
        }

        private T CreateInstance()
        {
            var instance = _factory.Create();
            _totalCreated++;
            _knownInstances.Add(instance);
            return instance;
        }

        /// <inheritdoc />
        public T Get()
        {
            T instance;

            if (_inactive.Count > 0)
            {
                instance = _inactive.Pop();
            }
            else if (_totalCreated >= _maxSize)
            {
                switch (_expansionMode)
                {
                    case PoolExpansionMode.Fixed:
                        throw new PoolExhaustedException(typeof(T), _maxSize);

                    case PoolExpansionMode.ExpandAndWarn:
                        instance = CreateInstance();
                        PoolExpanded?.Invoke(_totalCreated);
                        break;

                    case PoolExpansionMode.DynamicExpand:
                    default:
                        instance = CreateInstance();
                        break;
                }
            }
            else
            {
                instance = CreateInstance();
            }

            _activeSet.Add(instance);
            _onGet?.Invoke(instance);
            return instance;
        }

        /// <inheritdoc />
        public void Release(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (!_knownInstances.Contains(item))
            {
                InvalidReleaseDetected?.Invoke(item, InvalidReleaseReason.NotOwnedByPool);
                return;
            }

            if (!_activeSet.Remove(item))
            {
                InvalidReleaseDetected?.Invoke(item, InvalidReleaseReason.AlreadyReleased);
                return;
            }

            _onRelease?.Invoke(item);
            _inactive.Push(item);
        }

        /// <inheritdoc />
        public void Clear()
        {
            while (_inactive.Count > 0)
            {
                var instance = _inactive.Pop();
                _knownInstances.Remove(instance);
                _totalCreated--;
                _factory.Destroy(instance);
            }
        }
    }
}
