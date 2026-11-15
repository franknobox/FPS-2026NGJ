Shader "ART/CombatParticle"
{
    Properties
    {
        _MainTex ("粒子贴图", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("目标混合：1叠加/10透明", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color; float4 _MainTex_ST; float _DstBlend;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.color=v.color*_Color; o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            half4 frag(Varyings i):SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                // 叠加火光提高亮度，让亮沙地上也能读出枪焰；烟尘保留透明混合。
                c.rgb *= _DstBlend < 2 ? 3 : 1;
                return c;
            }
            ENDHLSL
        }
    }
}
