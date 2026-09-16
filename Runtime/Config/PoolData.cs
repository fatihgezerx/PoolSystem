using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoolSystem
{
    /// <summary>
    /// A single pooled prefab entry within a <see cref="PoolGroup"/>: which prefab to pool, the
    /// name it gets in the generated <c>PoolTypes</c> enum, and how many instances to create
    /// immediately when the game starts.
    /// </summary>
    [Serializable]
    public sealed class PoolEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private string typeName = string.Empty;
        [SerializeField] private int initializeCount = 1;

        /// <summary>The prefab this entry pools.</summary>
        public GameObject Prefab
        {
            get => prefab;
            set => prefab = value;
        }

        /// <summary>The name this entry gets as a member of the generated <c>PoolTypes</c> enum.</summary>
        public string TypeName
        {
            get => typeName;
            set => typeName = value;
        }

        /// <summary>How many instances to instantiate immediately when <c>PoolManager.Initialize</c> runs.</summary>
        public int InitializeCount
        {
            get => initializeCount;
            set => initializeCount = value;
        }
    }

    /// <summary>
    /// A named, designer-defined group of <see cref="PoolEntry"/> items (e.g. "Enemies", "Props").
    /// Purely organizational - grouping has no effect on runtime behavior, it only keeps a large
    /// data set readable in the Inspector.
    /// </summary>
    [Serializable]
    public sealed class PoolGroup
    {
        [SerializeField] private string header = "New Group";
        [SerializeField] private List<PoolEntry> entries = new();

        /// <summary>The group's display name in the Inspector.</summary>
        public string Header
        {
            get => header;
            set => header = value;
        }

        /// <summary>The pooled prefabs belonging to this group.</summary>
        public List<PoolEntry> Entries => entries;
    }

    /// <summary>
    /// A designer-facing definition of every pool in the project: a set of named <see cref="PoolGroup"/>s,
    /// each containing <see cref="PoolEntry"/> prefabs. Author this once as an asset, edit it entirely
    /// from its custom Inspector, and press "Compile" to generate the <c>PoolTypes</c> enum and wire
    /// up each prefab's <see cref="Poolable"/> component. At runtime, hand
    /// this asset to <c>PoolManager.Initialize</c> to prewarm every pool it describes.
    /// </summary>
    [CreateAssetMenu(menuName = "Pool System/Pool Data", fileName = "NewPoolData")]
    public sealed class PoolData : ScriptableObject
    {
        [SerializeField] private List<PoolGroup> groups = new();

        /// <summary>Every group in this data set.</summary>
        public List<PoolGroup> Groups => groups;
    }
}
