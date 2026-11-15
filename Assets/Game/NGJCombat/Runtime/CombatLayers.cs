using UnityEngine;

namespace NGJ.Combat
{
    // Resolve project layer names at call time, never in serialization constructors.
    public static class CombatLayers
    {
        public static int Enemy => LayerMask.NameToLayer("Enemy");
        public static int Player => LayerMask.NameToLayer("Player");
        public static int WorldMask => LayerMask.GetMask("Default", "Ground", "Object", "Interactable", "Grass", "Metal", "Mud", "Wood", "Ladder");
        public static int ActorMask => LayerMask.GetMask("Player", "Enemy");
        public static int PlayerAttackMask => WorldMask | LayerMask.GetMask("Enemy");
        public static int AttackMask => WorldMask | ActorMask;

        public static void SetActorLayer(GameObject root, int layer)
        {
            if (layer < 0) throw new System.InvalidOperationException("Missing combat layer in TagManager");
            root.layer = layer;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.gameObject.layer = layer;
        }
    }
}
