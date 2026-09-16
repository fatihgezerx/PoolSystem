using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// Pools <see cref="GameObject"/> instances cloned from a prefab. Wraps a <see cref="GenericPool{T}"/>
    /// of internal entries so that reference-back lookups (GameObject → cached <see cref="IPoolable"/>)
    /// never require a <c>GetComponent</c> call on the hot <see cref="Get"/>/<see cref="Release"/> path;
    /// components are resolved once, at instantiation time, and cached.
    /// </summary>
    public sealed class GameObjectPool : IObjectPool<GameObject>, IDisposable
    {
        /// <summary>
        /// Internal bookkeeping entry pairing a pooled GameObject with its (optionally null) cached
        /// <see cref="IPoolable"/> reference, resolved once at creation time.
        /// </summary>
        private sealed class Entry
        {
            public GameObject GameObject;
            public IPoolable Poolable;
        }

        private readonly GenericPool<Entry> _corePool;
        private readonly Dictionary<GameObject, Entry> _entryLookup;
        private readonly GameObject _prefab;
        private readonly Transform _poolContainer;
        private readonly bool _reparentOnRelease;

        /// <inheritdoc />
        public int Count => _corePool.Count;

        /// <inheritdoc />
        public int CountActive => _corePool.CountActive;

        /// <inheritdoc />
        public int CountInactive => _corePool.CountInactive;

        /// <summary>The prefab this pool instantiates clones of.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>The transform pooled (inactive) instances are parented under.</summary>
        public Transform PoolContainer => _poolContainer;

        /// <summary>
        /// Creates a new <see cref="GameObjectPool"/>.
        /// </summary>
        /// <param name="prefab">The prefab to clone. Must not be null.</param>
        /// <param name="poolContainer">
        /// Transform that inactive pooled instances are parented under, keeping the scene hierarchy tidy.
        /// Also used as the parent for newly instantiated GameObjects.
        /// </param>
        /// <param name="prewarmCount">Number of instances to eagerly instantiate up front.</param>
        /// <param name="maxSize">Capacity threshold; see <see cref="PoolExpansionMode"/>.</param>
        /// <param name="expansionMode">What to do when <paramref name="maxSize"/> is reached.</param>
        /// <param name="reparentOnRelease">
        /// If true, a released GameObject is re-parented back under <paramref name="poolContainer"/>.
        /// Set to false if you want to keep pooled-but-inactive objects wherever they were left
        /// (e.g. to preserve a world-space transform for debugging).
        /// </param>
        public GameObjectPool(
            GameObject prefab,
            Transform poolContainer,
            int prewarmCount = 0,
            int maxSize = int.MaxValue,
            PoolExpansionMode expansionMode = PoolExpansionMode.DynamicExpand,
            bool reparentOnRelease = true)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _poolContainer = poolContainer;
            _reparentOnRelease = reparentOnRelease;
            _entryLookup = new Dictionary<GameObject, Entry>();

            var factory = new DelegateObjectFactory<Entry>(CreateEntry, DestroyEntry);

            _corePool = new GenericPool<Entry>(
                factory,
                prewarmCount,
                maxSize,
                expansionMode,
                onGet: OnEntryGet,
                onRelease: OnEntryRelease);

            _corePool.InvalidReleaseDetected += OnInvalidRelease;
            _corePool.PoolExpanded += OnPoolExpanded;
        }

        private Entry CreateEntry()
        {
            var go = UnityEngine.Object.Instantiate(_prefab, _poolContainer);
            var entry = new Entry
            {
                GameObject = go,
                Poolable = go.GetComponent<IPoolable>()
            };
            go.SetActive(false);
            _entryLookup.Add(go, entry);
            return entry;
        }

        private void DestroyEntry(Entry entry)
        {
            _entryLookup.Remove(entry.GameObject);
            if (entry.GameObject != null)
            {
                UnityEngine.Object.Destroy(entry.GameObject);
            }
        }

        private static void OnEntryGet(Entry entry)
        {
            entry.GameObject.SetActive(true);
            entry.Poolable?.OnSpawned();
        }

        private void OnEntryRelease(Entry entry)
        {
            entry.Poolable?.OnDespawned();
            entry.GameObject.SetActive(false);
            if (_reparentOnRelease)
            {
                entry.GameObject.transform.SetParent(_poolContainer, false);
            }
        }

        private static void OnInvalidRelease(Entry entry, InvalidReleaseReason reason)
        {
            var name = entry?.GameObject != null ? entry.GameObject.name : "<null>";
            Debug.LogWarning($"[GameObjectPool] Rejected Release() for '{name}': {reason}.");
        }

        private void OnPoolExpanded(int newTotal)
        {
            Debug.LogWarning($"[GameObjectPool] Pool for prefab '{_prefab.name}' expanded past its configured " +
                              $"MaxSize; it now owns {newTotal} instances. Consider raising MaxSize.");
        }

        /// <inheritdoc />
        public GameObject Get()
        {
            var entry = _corePool.Get();
            return entry.GameObject;
        }

        /// <inheritdoc />
        public void Release(GameObject item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (!_entryLookup.TryGetValue(item, out var entry))
            {
                Debug.LogWarning($"[GameObjectPool] Attempted to release '{item.name}', which does not " +
                                  "belong to this pool.");
                return;
            }

            _corePool.Release(entry);
        }

        /// <inheritdoc />
        public void Clear() => _corePool.Clear();

        /// <summary>Clears the pool and unsubscribes from its internal events.</summary>
        public void Dispose()
        {
            _corePool.InvalidReleaseDetected -= OnInvalidRelease;
            _corePool.PoolExpanded -= OnPoolExpanded;
            Clear();
        }
    }
}
