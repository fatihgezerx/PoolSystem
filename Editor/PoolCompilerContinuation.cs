using System;
using UnityEditor;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// Finishes what <see cref="PoolCompiler.Compile"/> started. Its static constructor runs on
    /// every domain reload (including the one that follows generating a new <c>PoolTypes</c> enum),
    /// so it checks the <see cref="SessionState"/> flag <see cref="PoolCompiler.PendingFlagKey"/> and,
    /// if set, adds/updates a <see cref="Poolable"/> component on each prefab that was waiting on this
    /// compile, now that the freshly-generated enum members actually exist to assign.
    /// </summary>
    [InitializeOnLoad]
    internal static class PoolCompilerContinuation
    {
        static PoolCompilerContinuation()
        {
            if (!SessionState.GetBool(PoolCompiler.PendingFlagKey, false))
            {
                return;
            }

            // Consume the flag immediately so a crash mid-assignment can't loop forever.
            SessionState.SetBool(PoolCompiler.PendingFlagKey, false);

            // Deferred: right at static-constructor time, other systems (asset database, prefab
            // pipeline) may not have finished settling after the reload yet.
            EditorApplication.delayCall += RunPendingAssignments;
        }

        private static void RunPendingAssignments()
        {
            var json = SessionState.GetString(PoolCompiler.PendingPayloadKey, null);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var payload = JsonUtility.FromJson<PoolCompiler.PendingPayload>(json);
            var updated = 0;

            foreach (var item in payload.items)
            {
                if (TryAssignPrefab(item))
                {
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PoolCompiler] Compile finished: {updated}/{payload.items.Count} prefab(s) updated.");
        }

        private static bool TryAssignPrefab(PoolCompiler.PendingAssignment item)
        {
            var path = AssetDatabase.GUIDToAssetPath(item.prefabGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[PoolCompiler] Could not resolve prefab for GUID '{item.prefabGuid}' " +
                                $"(enum member '{item.enumMemberName}'). It may have been moved or deleted.");
                return false;
            }

            PoolTypes parsedType;
            try
            {
                parsedType = (PoolTypes)Enum.Parse(typeof(PoolTypes), item.enumMemberName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PoolCompiler] PoolTypes has no member '{item.enumMemberName}' " +
                                $"(prefab at '{path}'): {ex.Message}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var poolable = root.GetComponent<Poolable>();
                if (poolable == null)
                {
                    poolable = root.AddComponent<Poolable>();
                }

                poolable.poolType = parsedType;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
