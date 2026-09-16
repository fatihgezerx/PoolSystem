using System;

namespace PoolSystem
{
    /// <summary>
    /// Implement this on any pooled type to receive lifecycle notifications from the pool.
    /// These hooks are called by the pool instead of relying on Unity's <c>Awake</c>/<c>OnEnable</c>/
    /// <c>OnDisable</c> messages, because those fire on every <c>SetActive</c> toggle and cannot
    /// distinguish "spawned by the pool" from "re-enabled for an unrelated reason".
    /// </summary>
    /// <remarks>
    /// <see cref="OnDespawned"/> is the correct place to unsubscribe from events, cancel coroutines,
    /// clear cached state, or release references to other objects. Failing to do so is the most common
    /// source of leaks and "ghost" callbacks when pooled instances are reused.
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>
        /// Called by the pool immediately after this instance is returned from <c>Get()</c> and
        /// activated. Use this to reset state and (re-)subscribe to any events this instance needs
        /// while it is alive.
        /// </summary>
        void OnSpawned();

        /// <summary>
        /// Called by the pool immediately before this instance is deactivated and returned to the
        /// inactive pool via <c>Release()</c>. Use this to unsubscribe from events, stop coroutines,
        /// and release references so the instance does not keep external objects alive while idle.
        /// </summary>
        void OnDespawned();
    }
}
