using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PoolSystem
{
    /// <summary>
    /// The single component that makes a prefab poolable. Added automatically (and assigned its
    /// <see cref="PoolType"/>) by a <c>PoolData</c>'s "Compile" step - you normally never add this
    /// by hand.
    /// </summary>
    /// <remarks>
    /// This is a component you attach <i>alongside</i> your other scripts, not a base class you
    /// inherit from - a prefab that already has, say, a <c>GlassInteractable : MonoBehaviour</c>
    /// script does not need to be rewritten to also inherit some "poolable" base class (C# only
    /// allows one base class anyway). Instead, any sibling component that wants a spawn/despawn
    /// callback simply implements <see cref="IPoolable"/> directly - a plain interface has no
    /// inheritance cost - and <see cref="Poolable"/> discovers every such sibling once, at creation
    /// time, and forwards <see cref="OnSpawned"/>/<see cref="OnDespawned"/> to all of them. It also
    /// exposes <see cref="UnityEvent"/> hooks so a designer can wire simple reactions (a particle
    /// effect, a sound) from the Inspector without writing any code at all.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Poolable : MonoBehaviour, IPoolable
    {
        [Tooltip("Assigned automatically by PoolData's Compile step. Do not edit by hand.")]
        [SerializeField] internal PoolTypes poolType = PoolTypes.None;

        [Tooltip("Invoked after every sibling IPoolable.OnSpawned() call, for no-code Inspector wiring.")]
        [SerializeField] private UnityEvent onSpawned;

        [Tooltip("Invoked after every sibling IPoolable.OnDespawned() call, for no-code Inspector wiring.")]
        [SerializeField] private UnityEvent onDespawned;

        private IPoolable[] _siblingPoolables;

        /// <summary>The pool type this instance was compiled with. <see cref="PoolTypes.None"/> means it was never compiled.</summary>
        public PoolTypes PoolType => poolType;

        private void Awake()
        {
            // Resolved once, at creation time (Awake fires exactly once per physical instance,
            // regardless of how many times the pool later toggles SetActive) - never in the
            // Get()/Release() hot path.
            var all = GetComponents<IPoolable>();
            if (all.Length <= 1)
            {
                _siblingPoolables = Array.Empty<IPoolable>();
                return;
            }

            var siblings = new List<IPoolable>(all.Length - 1);
            for (var i = 0; i < all.Length; i++)
            {
                if (!ReferenceEquals(all[i], this))
                {
                    siblings.Add(all[i]);
                }
            }

            _siblingPoolables = siblings.ToArray();
        }

        /// <inheritdoc />
        public void OnSpawned()
        {
            for (var i = 0; i < _siblingPoolables.Length; i++)
            {
                _siblingPoolables[i].OnSpawned();
            }

            onSpawned?.Invoke();
        }

        /// <inheritdoc />
        public void OnDespawned()
        {
            for (var i = 0; i < _siblingPoolables.Length; i++)
            {
                _siblingPoolables[i].OnDespawned();
            }

            onDespawned?.Invoke();
        }

        /// <summary>Convenience for releasing this instance without looking up its pool manually.</summary>
        public void ReleaseSelf() => PoolManager.Release(poolType, gameObject);
    }
}
