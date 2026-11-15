using UnityEngine;
namespace SlugWhitebox
{
    [DefaultExecutionOrder(200)]
    public sealed class WeaponHandBinding : MonoBehaviour
    {
        public Transform gun,gripBone,leftShoulder,leftElbow,leftHand;
        public Vector3 positionOffset,leftSupport;
        public Quaternion rotationOffset=Quaternion.identity;
        public void ApplyPose()
        {
            if(!gun||!gripBone)return;
            gun.SetPositionAndRotation(gripBone.position+gripBone.rotation*positionOffset,gripBone.rotation*rotationOffset);
            if(!leftShoulder||!leftElbow||!leftHand)return;
            var target=gun.TransformPoint(leftSupport);
            // Keep the authored elbow's bend plane and solve a reachable two-bone arm.
            var shoulder=leftShoulder.position;var elbow=leftElbow.position;var wrist=leftHand.position;
            var wristRotation=leftHand.rotation;
            float upper=Vector3.Distance(shoulder,elbow),lower=Vector3.Distance(elbow,wrist);
            var delta=target-shoulder;float length=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.002f,upper+lower-.002f);
            var direction=delta.normalized;
            var pole=Vector3.ProjectOnPlane(elbow-shoulder,direction).normalized;
            if(pole.sqrMagnitude<.01f)pole=Vector3.ProjectOnPlane(-transform.up,direction).normalized;
            float along=(upper*upper+length*length-lower*lower)/(2*length);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var elbowTarget=shoulder+direction*along+pole*height;
            Rotate(leftShoulder,leftElbow.position,elbowTarget);
            Rotate(leftElbow,leftHand.position,shoulder+direction*length);
            leftHand.rotation=wristRotation;
        }
        static void Rotate(Transform joint,Vector3 hand,Vector3 target)
        {
            var a=hand-joint.position;var b=target-joint.position;
            if(a.sqrMagnitude>.00001f&&b.sqrMagnitude>.00001f)joint.rotation=Quaternion.FromToRotation(a,b)*joint.rotation;
        }
        void LateUpdate(){ApplyPose();}
    }
}
