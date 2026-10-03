# Changelog

## [1.3.0] - 2026-10-03

### Added
- `PoolManager.Shutdown()`: destroys every instance the pools created - waiting and handed out - and the `Pool [...]`
  container objects, unhooks the scene-unload handler and leaves `IsInitialized` false. Safe to call when not initialized. `ClearAll()` now calls it.

### Fixed
- Calling `PoolManager.Initialize` a second time no longer leaves the previous pools - and their container objects - behind;
  it shuts the old ones down first.
- With Enter Play Mode Options skipping the domain reload, `PoolManager` no longer keeps the previous session's (destroyed)
  pools or reports itself initialized when it is not.
- The pool containers were never destroyed when the pools were cleared; they are now. Instances still handed out when the
  pools are disposed were left behind; they are destroyed with their pool.

## [1.2.0] - 2026-10-03

### Added
- `PoolManager.Get(type, position, rotation, parent = null)` and `PoolManager.Get(type, position, eulerAngles,
  parent = null)`: take an instance and place it at a world position and rotation (rotation as a `Quaternion`
  or as Euler angles in a `Vector3`); it stays under the pool's container unless a parent is given. `GameObjectPool.Get(position, rotation,
  parent = null)` does the same for a single pool.

### Changed
- `GameObjectPool` activates an instance itself in `Get` instead of through the core pool's `onGet` callback,
  so a placed instance is positioned before `OnEnable` / `OnSpawned` run. Plain `Get()` behaves as before.

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
