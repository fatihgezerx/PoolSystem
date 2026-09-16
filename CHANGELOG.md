# Changelog

## [1.0.0] - 2026-09-16

### Added
- Pure C# pooling core with no UnityEngine dependency: `IObjectPool<T>`, `IPoolable`,
  `IPooledObjectFactory<T>`, `GenericPool<T>`, `PoolExpansionMode`, `InvalidReleaseReason`,
  `PoolExhaustedException`, `DelegateObjectFactory<T>`.
- Unity adapter: `GameObjectPool` (pools GameObjects, respects `IPoolable`) and `Poolable` (the
  component that makes a prefab poolable; discovers and forwards to any sibling `IPoolable` scripts,
  and exposes `OnSpawnedEvent`/`OnDespawnedEvent` `UnityEvent`s).
- `PoolManager`: static, array-indexed-by-enum entry point (`Initialize`, `Get`, `Release`,
  `ClearAll`), with automatic cleanup on scene unload.
- `PoolData` ScriptableObject (**Create > Pool System > Pool Data**): a designer-facing set of named
  groups, each holding prefab entries (prefab, name, initialize count), edited from a custom card-grid
  Inspector (`PoolDataEditor`).
- `PoolCompiler`: "Compile" button that generates a `PoolTypes` enum from the entries and assigns each
  prefab's `Poolable` component, in a domain-reload-safe two-phase process.
