using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SlugWhitebox
{
    // Only the asset's view model and clips; gameplay remains in SlugGame.
    public sealed class SlugWeaponView : MonoBehaviour
    {
        public Animator rig;
        public AnimationClip idleClip, fireClip;
        public Transform muzzle;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idle, fire;
        float firedAt = -100;
        public bool PlayingShot => graph.IsValid() && mixer.GetInputWeight(1) > 0 && fire.GetTime() > 0;

        void OnEnable()
        {
            if (!rig || !idleClip || !fireClip) return;
            graph = PlayableGraph.Create("Slug weapon: " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            idle = AnimationClipPlayable.Create(graph, idleClip);
            fire = AnimationClipPlayable.Create(graph, fireClip);
            idle.SetApplyFootIK(false); fire.SetApplyFootIK(false);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(idle, 0, mixer, 0); graph.Connect(fire, 0, mixer, 1);
            mixer.SetInputWeight(0, 1); mixer.SetInputWeight(1, 0);
            var output = AnimationPlayableOutput.Create(graph, "View model", rig);
            output.SetSourcePlayable(mixer);
            rig.applyRootMotion = false;
            firedAt = -100;
            graph.Play();
            graph.Evaluate(0);
        }

        public void Fire()
        {
            if (!graph.IsValid()) return;
            firedAt = Time.time;
            fire.SetTime(0); fire.SetDone(false);
            mixer.SetInputWeight(0, 0); mixer.SetInputWeight(1, 1);
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            if (idle.GetTime() >= idleClip.length) idle.SetTime(idle.GetTime() % idleClip.length);
            float remaining = fireClip.length - (Time.time - firedAt);
            float weight = Mathf.Clamp01(remaining / .09f);
            mixer.SetInputWeight(0, 1 - weight); mixer.SetInputWeight(1, weight);
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
