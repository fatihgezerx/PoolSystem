# Changelog

## [1.1.0] - 2026-09-24

### Added
- Drag-to-reorder groups in the `PoolData` Inspector: grab the handle on the left of a group's name
  and drop it anywhere in the list. A marker shows where it will land, and the move supports Undo.
- A **POOL SETTINGS** header box around the groups, with an info box explaining that group names only
  organize the Inspector, while the name under each prefab becomes its (unique) `PoolTypes` member.

### Changed
- Group names are edited in a compact bold text field (with a "Group name" placeholder when empty) and
  a matching smaller remove button, instead of the large free-standing title.
- Compile no longer re-saves prefabs whose `Poolable` already has the right `PoolType`; only new or
  changed prefabs are touched, avoiding needless prefab reimports on every Compile.

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
