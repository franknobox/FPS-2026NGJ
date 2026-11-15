using UnityEngine;
namespace NGJ.Combat
{
    public static class CombatFx
    {
        public static GameObject Spawn(GameObject prefab, Vector3 point, Quaternion rotation, Transform parent = null)
        {
            if (!prefab) return null;
            var effect = Object.Instantiate(prefab, point, rotation, parent);
            foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>()) particles.Play(false);
            Object.Destroy(effect, 5);
            return effect;
        }
        public static void Trail(TrailRenderer prefab, Vector3 start, Vector3 end)
        {
            if (!prefab) return;
            var trail = Object.Instantiate(prefab, start, Quaternion.identity);
            trail.Clear(); trail.AddPosition(start); trail.AddPosition(end); trail.emitting = false;
            Object.Destroy(trail.gameObject, Mathf.Max(.05f, trail.time));
        }
    }
}
