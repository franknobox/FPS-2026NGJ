using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtTools
{
    // Editor-only authoring settings; no runtime dependency on the showcase scene.
    public sealed class EnvironmentSizeProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [HideInInspector] public string prefabGuid;
            [InspectorName("场景素材")] public string label;
            [InspectorName("默认尺寸（米，含各级LOD）")] public Vector3 size;
        }
        public List<Entry> entries = new List<Entry>();
    }
}
