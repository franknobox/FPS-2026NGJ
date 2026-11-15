using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace ArtTools
{
    [CreateAssetMenu(menuName = "美术/漫画渲染风格", fileName = "ComicStyle")]
    public sealed class ComicStyleSettings : ScriptableObject
    {
        [Header("明暗与颜色")]
        [InspectorName("阴影颜色")] public Color shadowColor = new Color(.28f, .32f, .40f);
        [InspectorName("中间调颜色")] public Color midColor = new Color(.68f, .70f, .75f);
        [InspectorName("暗部分界"), Range(0, 1)] public float shadowThreshold = .22f;
        [InspectorName("亮部分界"), Range(0, 1)] public float lightThreshold = .65f;
        [InspectorName("色阶过渡"), Range(.001f, .2f)] public float bandSoftness = .035f;
        [InspectorName("环境光强度"), Range(0, 2)] public float ambientStrength = .65f;
        [InspectorName("环境光下限"), Range(0, .5f)] public float ambientFloor = .12f;
        [InspectorName("饱和度"), Range(0, 2)] public float saturation = 1.12f;
        [InspectorName("贴图对比度"), Range(.5f, 2)] public float contrast = 1.1f;
        [InspectorName("颜色量化级数 0关闭"), Range(0, 32)] public float colorLevels;
        [InspectorName("硬边高光强度"), Range(0, 1)] public float specularStrength = .2f;
        [InspectorName("高光分界"), Range(0, 1)] public float specularThreshold = .45f;
        [InspectorName("法线强度"), Range(0, 2)] public float normalStrength = .6f;
        [Header("排线")]
        [InspectorName("排线强度"), Range(0, .5f)] public float hatchStrength = .05f;
        [InspectorName("排线密度"), Range(4, 160)] public float hatchScale = 70;
        [Header("屏幕墨线")]
        [InspectorName("启用描边")] public bool enableInk = true;
        [InspectorName("墨线颜色")] public Color inkColor = new Color(.015f, .012f, .018f, 1);
        [InspectorName("描边像素宽度"), Range(.25f, 4)] public float inkWidth = 1.2f;
        [InspectorName("轮廓线阈值"), Range(.001f, .1f)] public float depthThreshold = .015f;
        [InspectorName("结构线阈值"), Range(.05f, 1)] public float normalThreshold = .32f;
        [InspectorName("结构线强度"), Range(0, 1)] public float normalStrengthInk = .65f;
        [InspectorName("结构线淡出距离"), Min(1)] public float normalFadeDistance = 30;
        [Header("导入流程")]
        [InspectorName("自动处理新增ART材质")] public bool autoStyleNewArtMaterials = true;
        [HideInInspector] public List<ComicPipelineBinding> rendererBindings = new List<ComicPipelineBinding>();
        [HideInInspector] public List<Material> autoStyleExclusions = new List<Material>();
    }

    [Serializable]
    public sealed class ComicPipelineBinding
    {
        public UniversalRenderPipelineAsset pipeline;
        public ScriptableRendererData[] originalRenderers;
    }
}
