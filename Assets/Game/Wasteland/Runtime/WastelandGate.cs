using UnityEngine;

namespace Wasteland
{
    public sealed class WastelandGate : MonoBehaviour
    {
        public GameObject barrier;
        public Renderer indicator;
        public Material openMaterial, closedMaterial;
        public bool Closed { get; private set; }
        public void SetClosed(bool closed)
        {
            Closed = closed;
            if (barrier) barrier.SetActive(closed);
            if (indicator) indicator.sharedMaterial = closed ? closedMaterial : openMaterial;
        }
    }
}
