using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// Implements the "Compile" step for a <see cref="PoolData"/>. Compiling has to survive a
    /// script domain reload, because the very thing it does is generate a brand new <c>PoolTypes</c>
    /// enum member for every entry - and that enum only exists in memory <i>after</i> Unity
    /// finishes recompiling. So compiling happens in two phases:
    /// <list type="number">
    /// <item>Now: generate <c>PoolTypes.cs</c> from the current entries, stash which prefab
    /// should get which enum member in <see cref="SessionState"/> (which survives a domain reload
    /// within the same Editor session, unlike a static field), and trigger a recompile.</item>
    /// <item>After reload: <see cref="PoolCompilerContinuation"/>'s static constructor notices the
    /// pending flag, adds/updates a <c>Poolable</c> component on each stashed prefab, and
    /// assigns its <c>PoolType</c> now that the enum member actually exists.</item>
    /// </list>
    /// </summary>
    internal static class PoolCompiler
    {
        private const string GeneratedFileRelativePath = "Assets/Scripts/PoolSystem/Runtime/Generated/PoolTypes.cs";
        internal const string PendingFlagKey = "PoolSystem.PendingCompile";
        internal const string PendingPayloadKey = "PoolSystem.PendingCompilePayload";

        [Serializable]
        internal sealed class PendingAssignment
        {
            public string prefabGuid;
            public string enumMemberName;
        }

        [Serializable]
        internal sealed class PendingPayload
        {
            public List<PendingAssignment> items = new();
        }

        /// <summary>
        /// Validates every entry in <paramref name="poolData"/>, (re)generates the <c>PoolTypes</c>
        /// enum, and schedules each prefab to be assigned its new enum member once Unity finishes
        /// recompiling.
        /// </summary>
        public static void Compile(PoolData poolData)
        {
            var payload = new PendingPayload();
            var enumMemberNames = new List<string>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in poolData.Groups)
            {
                foreach (var entry in group.Entries)
                {
                    if (entry.Prefab == null)
                    {
                        Debug.LogWarning($"[PoolCompiler] Skipping an entry with no prefab in group '{group.Header}'.");
                        continue;
                    }

                    var sanitized = SanitizeIdentifier(entry.TypeName);
                    if (sanitized == null)
                    {
                        Debug.LogError($"[PoolCompiler] Entry for prefab '{entry.Prefab.name}' in group " +
                                        $"'{group.Header}' has an invalid or empty name ('{entry.TypeName}'). " +
                                        "Compile aborted - fix it and try again.");
                        return;
                    }

                    if (sanitized == "None")
                    {
                        Debug.LogError($"[PoolCompiler] Entry for prefab '{entry.Prefab.name}' in group " +
                                        $"'{group.Header}' is named 'None', which is reserved for the " +
                                        "\"unassigned\" sentinel value. Rename it. Compile aborted.");
                        return;
                    }

                    if (!seenNames.Add(sanitized))
                    {
                        Debug.LogError($"[PoolCompiler] Duplicate pool type name '{sanitized}' (from " +
                                        $"'{entry.TypeName}') on prefab '{entry.Prefab.name}'. Every entry " +
                                        "needs a unique name. Compile aborted.");
                        return;
                    }

                    var prefabPath = AssetDatabase.GetAssetPath(entry.Prefab);
                    var guid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogError($"[PoolCompiler] Could not resolve an asset GUID for prefab " +
                                        $"'{entry.Prefab.name}'. Is it a saved prefab asset? Compile aborted.");
                        return;
                    }

                    enumMemberNames.Add(sanitized);
                    payload.items.Add(new PendingAssignment { prefabGuid = guid, enumMemberName = sanitized });
                }
            }

            WriteEnumSource(enumMemberNames);

            SessionState.SetString(PendingPayloadKey, JsonUtility.ToJson(payload));
            SessionState.SetBool(PendingFlagKey, true);

            AssetDatabase.Refresh();
            Debug.Log($"[PoolCompiler] Generated PoolTypes with {enumMemberNames.Count} entr" +
                      $"{(enumMemberNames.Count == 1 ? "y" : "ies")}. Waiting for scripts to recompile " +
                      "before assigning prefabs...");
        }

        private static void WriteEnumSource(List<string> memberNames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by PoolCompiler.Compile(). Do not edit by hand - it is overwritten");
            sb.AppendLine("// every time a PoolData asset is compiled.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("namespace PoolSystem");
            sb.AppendLine("{");
            sb.AppendLine("    public enum PoolTypes");
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");
            foreach (var name in memberNames)
            {
                sb.AppendLine($"        {name},");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var fullPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                GeneratedFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, sb.ToString());
        }

        private static string SanitizeIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var c in raw.Trim())
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            if (sb.Length == 0)
            {
                return null;
            }

            if (char.IsDigit(sb[0]))
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }
    }
}
