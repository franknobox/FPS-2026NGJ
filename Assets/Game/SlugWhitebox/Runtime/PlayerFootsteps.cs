using UnityEngine;
namespace SlugWhitebox
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        public AudioClip[] clips;
        [Min(.2f)] public float stride = 1.9f;
        [Range(0,1)] public float volume = .5f;
        public SlugGame game;
        CharacterController motor;
        AudioSource audioSource;
        Vector3 previous;
        float distance;
        int last=-1;
        void Awake()
        {
            motor=GetComponent<CharacterController>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.playOnAwake=false;
            previous=transform.position;
        }
        public int PickClip()
        {
            if(clips==null||clips.Length==0)return -1;
            int index=last<0?Random.Range(0,clips.Length):clips.Length==1?0:Random.Range(0,clips.Length-1);
            if(clips.Length>1&&last>=0&&index>=last)index++;
            last=index;return index;
        }
        void LateUpdate()
        {
            float traveled=Vector3.ProjectOnPlane(transform.position-previous,Vector3.up).magnitude;
            previous=transform.position;
            if(!game||!game.Running||!motor.isGrounded||traveled>2||traveled<.001f){distance=0;return;}
            distance+=traveled;if(distance<stride)return;distance%=stride;
            int index=PickClip();if(index<0||!clips[index])return;
            audioSource.pitch=Random.Range(.94f,1.06f);audioSource.PlayOneShot(clips[index],volume);
        }
        void OnDisable(){distance=0;previous=transform.position;if(audioSource)audioSource.Stop();}
    }
}
