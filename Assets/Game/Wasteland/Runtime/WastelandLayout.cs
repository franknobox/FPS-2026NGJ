using System;
using UnityEngine;

namespace Wasteland
{
    [Serializable]
    public sealed class WastelandArea
    {
        public string displayName;
        public Bounds bounds;
        public WastelandGate entrance, exit;
        public Transform[] groundSpawns, airSpawns;
        public int firstWave = 4, secondWave = 4;
        public bool Contains(Vector3 point, float inset = 0)
        {
            return point.x >= bounds.min.x + inset && point.x <= bounds.max.x - inset &&
                   point.z >= bounds.min.z + inset && point.z <= bounds.max.z - inset;
        }
    }
}
