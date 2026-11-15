using UnityEngine;
namespace SlugWhitebox
{
    [CreateAssetMenu(menuName="循声虫族/移动与拾取配置")]
    public class SlugTuning : ScriptableObject
    {
        [Min(.1f)] public float moveSpeed=4;
        [Min(.001f)] public float mouseSensitivity=.095f;
        [Min(0)] public int biomassDrop=10;
        [Min(.1f)] public float pickupRadius=4;
    }
}
