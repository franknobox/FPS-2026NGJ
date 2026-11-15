using System;
using System.Linq;
using SlugWhitebox;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NGJ.EditorTools
{
    public static class WeaponFeelSetup
    {
        [MenuItem("NGJ/美术/应用枪声与火箭弹尺寸")]
        public static void Apply()
        {
            if(UnityEditor.EditorApplication.isPlaying)throw new Exception("Only edit mode");
            string shotgun="Assets/Game/Prefabs/Weapons/Shotgun.prefab";
            var gun=PrefabUtility.LoadPrefabContents(shotgun);
            try{gun.GetComponent<SlugWeaponView>().shotVolume=.18f;PrefabUtility.SaveAsPrefabAsset(gun,shotgun);}
            finally{PrefabUtility.UnloadPrefabContents(gun);}
            string path="Assets/Game/NGJCombat/Generated/Prefabs/RocketProjectile.prefab";
            var rocket=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual=rocket.transform.GetChild(0);
                var renderers=visual.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)&&!(r is TrailRenderer)&&!(r is LineRenderer)).ToArray();
                if(renderers.Length==0)throw new Exception("Rocket mesh missing");
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                float factor=.13f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                visual.localScale*=factor;visual.localPosition*=factor;
                rocket.GetComponent<NGJ.Combat.CombatProjectile>().radius=.025f;
                ConfigureRocketParticles(visual.gameObject);
                PrefabUtility.SaveAsPrefabAsset(rocket,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(rocket);}
            AssetDatabase.SaveAssets();
        }
        public static void ConfigureRocketParticles(GameObject model)
        {
            foreach(var ps in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=ps.main;main.scalingMode=ParticleSystemScalingMode.Shape;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startSize=.025f;main.startSpeed=.08f;main.startLifetime=.22f;main.maxParticles=20;
                var shape=ps.shape;shape.enabled=false;var size=ps.sizeOverLifetime;size.enabled=false;
                var velocity=ps.velocityOverLifetime;velocity.enabled=false;var force=ps.forceOverLifetime;force.enabled=false;
                var noise=ps.noise;noise.enabled=false;var trails=ps.trails;trails.enabled=false;
                var emission=ps.emission;emission.rateOverTime=30;emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            }
            foreach(var trail in model.GetComponentsInChildren<TrailRenderer>(true)){trail.widthMultiplier=.018f;trail.time=.12f;}
        }
    }
}
