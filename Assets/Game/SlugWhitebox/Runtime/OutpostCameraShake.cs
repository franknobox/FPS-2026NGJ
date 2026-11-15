using UnityEngine;
using NGJ.Combat;

namespace SlugWhitebox
{
    // 只偏移镜头；每帧先还原，避免震动积累导致瞄准漂移。
    [DefaultExecutionOrder(-200)]
    public sealed class OutpostCameraShake : MonoBehaviour
    {
        [Range(0,2), Tooltip("总强度，设为 0 关闭屏幕震动。")]
        public float intensity = 1;
        [Min(0)] public float positionAmount = .055f;
        [Min(0)] public float rotationDegrees = .65f;
        [Min(1)] public float frequency = 26;
        [Min(.1f)] public float explosionRange = 24;
        [Tooltip("步枪、狙击、霰弹、榴弹、火箭筒的开火力度。")]
        public float[] weaponStrength = { .35f,.85f,.75f,.6f,1f };
        public SlugGame game;
        Transform target;
        Vector3 offset;
        Quaternion rotationOffset = Quaternion.identity;
        float remaining, duration, strength;
        public bool IsShaking => remaining > 0 && intensity > 0;
        public float LastImpulse { get; private set; }
        public void WeaponImpulse(int index)
        {
            if(weaponStrength != null && weaponStrength.Length > 0)
                Impulse(weaponStrength[Mathf.Clamp(index,0,weaponStrength.Length-1)]);
        }
        void OnEnable() => CombatProjectile.ExplosionOccurred += OnExplosion;
        void OnDisable() { CombatProjectile.ExplosionOccurred -= OnExplosion; ResetShake(); }
        void Update() => RemoveOffset();
        void RemoveOffset()
        {
            if(target) { target.localPosition-=offset; target.localRotation*=Quaternion.Inverse(rotationOffset); }
            offset=Vector3.zero; rotationOffset=Quaternion.identity;
        }
        public void ResetShake() { RemoveOffset(); remaining=0; strength=0; LastImpulse=0; }
        public void Impulse(float amount, float seconds=.18f)
        {
            if(!game || !game.Running || amount<=0) return;
            // 连射取较强的一次，不无限叠加。
            strength=Mathf.Max(strength,Mathf.Clamp(amount,0,1.5f));
            remaining=Mathf.Max(remaining,seconds); duration=remaining; LastImpulse=amount;
        }
        void OnExplosion(Vector3 point, float radius)
        {
            if(!game) return;
            float range=Mathf.Max(explosionRange,radius*3);
            float attenuation=Mathf.Clamp01(1-Vector3.Distance(game.PlayerPosition,point)/range);
            if(attenuation>0) Impulse(attenuation*1.4f,.38f);
        }
        void LateUpdate()
        {
            if(!game || !game.Running) { ResetShake(); return; }
            target=game.view.transform;
            if(remaining<=0) return;
            remaining=Mathf.Max(0,remaining-Time.deltaTime);
            float envelope=remaining/Mathf.Max(.01f,duration);
            float amplitude=strength*envelope*envelope*intensity;
            float t=Time.time*frequency;
            Vector3 noise=new Vector3(Mathf.PerlinNoise(t,7)*2-1,Mathf.PerlinNoise(13,t)*2-1,Mathf.PerlinNoise(t,29)*2-1);
            offset=Vector3.Scale(noise,new Vector3(1,1,.3f))*positionAmount*amplitude;
            rotationOffset=Quaternion.Euler(noise.y*rotationDegrees*amplitude,noise.x*rotationDegrees*amplitude,noise.z*rotationDegrees*amplitude*.6f);
            target.localPosition+=offset; target.localRotation*=rotationOffset;
            if(remaining<=0) strength=0;
        }
    }
}
