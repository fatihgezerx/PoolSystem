using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoolSystem
{
    /// <summary>
    /// Static entry point for the enum-driven pooling workflow. Call <see cref="Initialize"/> once
    /// (e.g. from a bootstrap <c>MonoBehaviour</c>'s <c>Awake</c>) with a compiled <see cref="PoolData"/>,
    /// then use <see cref="Get(PoolTypes)"/>/<see cref="Release(PoolTypes,GameObject)"/> anywhere.
    /// </summary>
    /// <remarks>
    /// Backed by a plain array indexed by <c>(int)PoolTypes</c> rather than a <c>Dictionary&lt;PoolTypes,_&gt;</c>.
    /// Because <c>PoolTypes</c> is generated as a contiguous 0..N-1 enum (see <c>PoolCompiler</c>), a direct
    /// array index is both faster and allocation-free compared to hashing an enum key (which also boxes
    /// on older runtimes without a custom comparer) - the fastest possible lookup for this shape of problem.
    /// </remarks>
    public static class PoolManager
    {
        private static GameObjectPool[] _pools;
        private static bool _sceneUnloadHookInstalled;

        /// <summary>Whether <see cref="Initialize"/> has been called and pools are ready to use.</summary>
        public static bool IsInitialized => _pools != null;

        /// <summary>
        /// When true (the default), every pool is automatically cleared when a scene unloads,
        /// preventing stale <see cref="GameObjectPool"/> references from leaking.
        /// </summary>
        public static bool AutoClearOnSceneUnload { get; set; } = true;

        /// <summary>
        /// Builds one <see cref="GameObjectPool"/> per entry in <paramref name="poolData"/>, prewarming
        /// each with its configured <c>InitializeCount</c>. Every prefab must already have a
        /// <see cref="Poolable"/> component with a valid (non-<see cref="PoolTypes.None"/>)
        /// <see cref="Poolable.PoolType"/> - run "Compile" on the data asset first if it doesn't.
        /// </summary>
        public static void Initialize(PoolData poolData)
        {
            if (poolData == null) throw new ArgumentNullException(nameof(poolData));

            var typeCount = Enum.GetValues(typeof(PoolTypes)).Length;
            _pools = new GameObjectPool[typeCount];

            foreach (var group in poolData.Groups)
            {
                foreach (var entry in group.Entries)
                {
                    RegisterEntry(entry);
                }
            }

            if (!_sceneUnloadHookInstalled)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _sceneUnloadHookInstalled = true;
            }
        }

        private static void RegisterEntry(PoolEntry entry)
        {
            if (entry.Prefab == null)
            {
                Debug.LogWarning("[PoolManager] Skipping an entry with no prefab assigned.");
                return;
            }

            var poolable = entry.Prefab.GetComponent<Poolable>();
            if (poolable == null || poolable.PoolType == PoolTypes.None)
            {
                Debug.LogError($"[PoolManager] Prefab '{entry.Prefab.name}' has no compiled Poolable " +
                                "component. Open its PoolData asset and press Compile first.");
                return;
            }

            var index = (int)poolable.PoolType;
            if (_pools[index] != null)
            {
                Debug.LogWarning($"[PoolManager] '{poolable.PoolType}' is already registered; ignoring " +
                                  $"the duplicate entry on prefab '{entry.Prefab.name}'.");
                return;
            }

            var container = new GameObject($"Pool [{poolable.PoolType}]").transform;
            _pools[index] = new GameObjectPool(
                entry.Prefab,
                container,
                entry.InitializeCount,
                int.MaxValue,
                PoolExpansionMode.DynamicExpand);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (AutoClearOnSceneUnload)
            {
                ClearAll();
            }
        }

        /// <summary>Retrieves an instance of <paramref name="type"/> from its pool.</summary>
        public static GameObject Get(PoolTypes type) => ResolvePool(type).Get();

        /// <summary>Returns <paramref name="instance"/> to the pool for <paramref name="type"/>.</summary>
        public static void Release(PoolTypes type, GameObject instance) => ResolvePool(type).Release(instance);

        /// <summary>
        /// Convenience overload that reads the pool type off <paramref name="instance"/>'s
        /// <see cref="Poolable"/> component. Prefer <see cref="Release(PoolTypes,GameObject)"/> or
        /// <see cref="Poolable.ReleaseSelf"/> in hot paths to avoid this extra <c>GetComponent</c> call.
        /// </summary>
        public static void Release(GameObject instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var poolable = instance.GetComponent<Poolable>();
            if (poolable == null)
            {
                Debug.LogWarning($"[PoolManager] '{instance.name}' has no Poolable component; cannot release it.");
                return;
            }

            Release(poolable.PoolType, instance);
        }

        private static GameObjectPool ResolvePool(PoolTypes type)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("[PoolManager] Not initialized. Call PoolManager.Initialize(poolData) first.");
            }

            var pool = _pools[(int)type];
            if (pool == null)
            {
                throw new InvalidOperationException($"[PoolManager] No pool is registered for '{type}'.");
            }

            return pool;
        }

        /// <summary>Clears and discards every currently registered pool.</summary>
        public static void ClearAll()
        {
            if (_pools == null)
            {
                return;
            }

            foreach (var pool in _pools)
            {
                pool?.Dispose();
            }

            _pools = null;
        }

        /// <summary>Every currently registered (type, pool) pair. For diagnostics/tooling use.</summary>
        public static IEnumerable<KeyValuePair<PoolTypes, GameObjectPool>> GetAllPools()
        {
            if (_pools == null)
            {
                yield break;
            }

            for (var i = 0; i < _pools.Length; i++)
            {
                if (_pools[i] != null)
                {
                    yield return new KeyValuePair<PoolTypes, GameObjectPool>((PoolTypes)i, _pools[i]);
                }
            }
        }
    }
}
