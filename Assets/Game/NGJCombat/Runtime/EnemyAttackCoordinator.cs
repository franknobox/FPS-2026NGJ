using System.Collections.Generic;
using UnityEngine;
namespace NGJ.Combat
{
    public sealed class EnemyAttackCoordinator : MonoBehaviour
    {
        [Min(1)] public int maxConcurrent=2;
        [Min(.1f)] public float startSpacing=.85f;
        readonly HashSet<EnemyCombatant> attackers=new HashSet<EnemyCombatant>();
        float nextStart;
        public int ActiveCount=>attackers.Count;
        public bool TryAcquire(EnemyCombatant enemy)
        {
            attackers.RemoveWhere(e=>!e||!e.IsAlive||!e.isActiveAndEnabled||(!e.IsAttacking&&!e.IsWindingUp));
            if(attackers.Contains(enemy))return true;
            if(Time.time<nextStart||attackers.Count>=maxConcurrent)return false;
            if(enemy.definition.kind==EnemyKind.ZhenYin)
                foreach(var actor in attackers)if(actor.definition.kind==EnemyKind.ZhenYin)return false;
            attackers.Add(enemy);nextStart=Time.time+startSpacing;return true;
        }
        public void Release(EnemyCombatant enemy){attackers.Remove(enemy);}
        public void ResetSchedule(){attackers.Clear();nextStart=Time.time+.7f;}
    }
}
