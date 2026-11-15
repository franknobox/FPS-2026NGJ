Shader "Hidden/ART/Comic/Screen Ink"
{
    Properties
    {
        _InkColor("墨线颜色", Color) = (.015,.012,.018,1)
        _InkWidth("描边像素宽度", Range(.25,4)) = 1.2
        _DepthThreshold("深度边缘阈值", Range(.001,.1)) = .015
        _NormalThreshold("结构线阈值", Range(.05,1)) = .32
        _NormalStrength("结构线强度", Range(0,1)) = .65
        _NormalFadeDistance("结构线淡出距离", Float) = 30
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ScreenInk"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment InkFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float _InkWidth,_DepthThreshold,_NormalThreshold,_NormalStrength,_NormalFadeDistance;
            CBUFFER_END
            float InkEyeDepth(float2 uv)
            {
                float raw=SampleSceneDepth(saturate(uv));
                if (unity_OrthoParams.w>.5)
                {
                    #if UNITY_REVERSED_Z
                        raw=1-raw;
                    #endif
                    return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                }
                return LinearEyeDepth(raw,_ZBufferParams);
            }
            half4 InkFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float2 pixel=_InkWidth/_ScaledScreenParams.xy;
                float center=InkEyeDepth(uv);
                half3 normal=SampleSceneNormals(uv);
                float depthEdge=0,normalEdge=0;
                float2 offsets[4]={float2(pixel.x,0),float2(-pixel.x,0),float2(0,pixel.y),float2(0,-pixel.y)};
                UNITY_UNROLL for(int i=0;i<4;i++)
                {
                    float2 neighbour=saturate(uv+offsets[i]);
                    float d=InkEyeDepth(neighbour);
                    depthEdge=max(depthEdge,abs(center-d)/max(min(center,d),.05));
                    half3 n=SampleSceneNormals(neighbour);
                    normalEdge=max(normalEdge,1-saturate(dot(normal,n)));
                }
                float silhouette=smoothstep(_DepthThreshold,_DepthThreshold*2,depthEdge);
                float structure=smoothstep(_NormalThreshold,_NormalThreshold+.15,normalEdge)*_NormalStrength;
                structure*=saturate(1-center/max(_NormalFadeDistance,.1));
                // Sky normals are undefined; only depth should ink the sky boundary.
                structure*=step(center,_ProjectionParams.z*.98);
                half ink=saturate(max(silhouette,structure))*_InkColor.a;
                color.rgb=lerp(color.rgb,_InkColor.rgb,ink);
                return color;
            }
            ENDHLSL
        }
    }
}
