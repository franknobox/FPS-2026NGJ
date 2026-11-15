using UnityEngine;

namespace SlugWhitebox
{
    public abstract class SlugLevelFlow : MonoBehaviour
    {
        public abstract Vector3 StartPosition { get; }
        public virtual string Title => "循声虫族";
        public virtual int AreaCount => 3;
        public abstract string ProgressText { get; }
        public abstract void ResetRun(SlugGame game);
        public abstract void Tick(SlugGame game);
        public virtual void OnEnemyKilled(SlugGame game, SlugEnemy enemy) { }
        public virtual void StopRun() { }
    }
}
