Shader "ART/Comic/Toon Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("颜色贴图", 2D) = "white" {}
        [MainColor] _BaseColor("颜色", Color) = (1,1,1,1)
        [Normal] _BumpMap("法线贴图", 2D) = "bump" {}
        _BumpScale("法线强度", Range(0,2)) = 0.6
        _MetallicGlossMap("金属度 R / 光滑度 A", 2D) = "white" {}
        _Metallic("金属度", Range(0,1)) = 0
        _Smoothness("光滑度", Range(0,1)) = 0.5
        _OcclusionMap("遮蔽贴图 G", 2D) = "white" {}
        _OcclusionStrength("遮蔽强度", Range(0,1)) = 1
        _EmissionMap("自发光贴图", 2D) = "white" {}
        [HDR] _EmissionColor("自发光颜色", Color) = (0,0,0,0)
        [Header(Comic Lighting)]
        _ShadowColor("阴影色", Color) = (0.28,0.32,0.40,1)
        _MidColor("中间调", Color) = (0.68,0.70,0.75,1)
        _ShadowThreshold("暗部分界", Range(0,1)) = 0.22
        _LightThreshold("亮部分界", Range(0,1)) = 0.65
        _BandSoftness("色阶过渡", Range(0.001,0.2)) = 0.035
        _AmbientStrength("环境光强度", Range(0,2)) = 0.65
        _AmbientFloor("环境光下限", Range(0,0.5)) = 0.12
        _SpecularStrength("硬边高光强度", Range(0,1)) = 0.2
        _SpecularThreshold("高光分界", Range(0,1)) = 0.45
        _Saturation("饱和度", Range(0,2)) = 1.12
        _Contrast("贴图对比度", Range(0.5,2)) = 1.1
        _ColorLevels("颜色量化级数 0 关闭", Range(0,32)) = 0
        _HatchStrength("暗部排线强度", Range(0,0.5)) = 0.05
        _HatchScale("排线密度", Range(4,160)) = 70
        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("透明裁切", Float) = 0
        _Cutoff("裁切阈值", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("背面剔除", Float) = 2
        [Toggle] _WorldSpaceUV("世界坐标贴图", Float) = 0
        _UVs("世界坐标贴图尺寸", Float) = 1
        [Toggle] _VertexTint("顶点地表染色", Float) = 0
        [HideInInspector] _MainTex("Legacy texture", 2D) = "white" {}
        [HideInInspector] _Color("Legacy color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _ShadowColor, _MidColor, _Color;
            half _BumpScale, _Metallic, _Smoothness, _OcclusionStrength;
            half _ShadowThreshold, _LightThreshold, _BandSoftness, _AmbientStrength;
            half _AmbientFloor, _SpecularStrength, _SpecularThreshold, _Saturation;
            half _Contrast, _ColorLevels, _HatchStrength, _HatchScale;
            half _AlphaClip, _Cutoff, _Cull, _WorldSpaceUV;
            float _UVs;
            half _VertexTint;
        CBUFFER_END
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);

        half4 ComicBase(float2 uv, float3 positionWS, half3 normalWS)
        {
            if (_WorldSpaceUV > 0.5)
            {
                float3 p = positionWS / -max(abs(_UVs),0.0001);
                half4 x = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz);
                half4 y = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz);
                half4 z = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy);
                return lerp(lerp(y,x,abs(normalWS.x)),z,abs(normalWS.z)) * _BaseColor;
            }
            return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv) * _BaseColor;
        }
        ENDHLSL

        Pass
        {
            Name "ComicForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ComicVertex
            #pragma fragment ComicFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD0;
                float2 lightmapUV:TEXCOORD1;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                half3 normalWS:TEXCOORD2;
                half4 tangentWS:TEXCOORD3;
                half4 fogAndVertexLight:TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV,vertexSH,5);
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings ComicVertex(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                output.positionCS=position.positionCS;
                output.positionWS=position.positionWS;
                output.normalWS=normal.normalWS;
                output.tangentWS=half4(normal.tangentWS,input.tangentOS.w*GetOddNegativeScale());
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                output.color=input.color;
                output.fogAndVertexLight=half4(ComputeFogFactor(position.positionCS.z),VertexLighting(position.positionWS,normal.normalWS));
                OUTPUT_LIGHTMAP_UV(input.lightmapUV,unity_LightmapST,output.lightmapUV);
                OUTPUT_SH(normal.normalWS,output.vertexSH);
                return output;
            }
            half3 ComicLight(Light light,half3 normalWS,half3 viewWS,half3 albedo,half metallic,half smoothness)
            {
                half ndl=saturate(dot(normalWS,light.direction));
                half softness=max(_BandSoftness,(half)0.001);
                half mid=smoothstep(_ShadowThreshold-softness,_ShadowThreshold+softness,ndl);
                half lit=smoothstep(_LightThreshold-softness,_LightThreshold+softness,ndl);
                half3 band=lerp(lerp(_ShadowColor.rgb,_MidColor.rgb,mid),half3(1,1,1),lit);
                band=lerp(_ShadowColor.rgb,band,light.shadowAttenuation);
                half3 halfDirection=SafeNormalize(light.direction+viewWS);
                half spec=pow(saturate(dot(normalWS,halfDirection)),lerp(20.0h,100.0h,smoothness));
                spec=smoothstep(_SpecularThreshold-softness,_SpecularThreshold+softness,spec);
                half3 specColor=lerp(half3(1,1,1),albedo,metallic);
                return (albedo*band+specColor*spec*_SpecularStrength*lit*light.shadowAttenuation)*light.color*light.distanceAttenuation;
            }
            half4 ComicFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 geometricNormal=NormalizeNormalPerPixel(input.normalWS);
                half4 base=ComicBase(input.uv,input.positionWS,geometricNormal);
                base.rgb*=lerp(half3(1,1,1),input.color.rgb,_VertexTint);
                #if defined(_ALPHATEST_ON)
                    clip(base.a-_Cutoff);
                #endif
                half3 albedo=max(0,(base.rgb-0.5h)*_Contrast+0.5h);
                half luminance=dot(albedo,half3(.2126h,.7152h,.0722h));
                albedo=max(0,lerp(luminance.xxx,albedo,_Saturation));
                if (_ColorLevels>1.5h) albedo=floor(saturate(albedo)*_ColorLevels+0.5h)/_ColorLevels;
                half3 normalWS=geometricNormal;
                #if defined(_NORMALMAP)
                    if (_WorldSpaceUV<0.5h)
                    {
                        half3 bitangent=input.tangentWS.w*cross(geometricNormal,input.tangentWS.xyz);
                        half3 normalTS=SampleNormal(input.uv,TEXTURE2D_ARGS(_BumpMap,sampler_BumpMap),_BumpScale);
                        normalWS=NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,half3x3(input.tangentWS.xyz,bitangent,geometricNormal)));
                    }
                #endif
                half metallic=_Metallic;
                half smoothness=_Smoothness;
                #if defined(_METALLICSPECGLOSSMAP)
                    half4 mr=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,input.uv);
                    metallic*=mr.r; smoothness*=mr.a;
                #endif
                InputData inputData=(InputData)0;
                inputData.positionWS=input.positionWS;
                inputData.normalWS=normalWS;
                inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    inputData.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #endif
                half4 shadowMask=SAMPLE_SHADOWMASK(input.lightmapUV);
                Light mainLight=GetMainLight(inputData.shadowCoord,input.positionWS,shadowMask);
                half3 gi=SAMPLE_GI(input.lightmapUV,input.vertexSH,normalWS);
                half3 color=albedo*max(gi*_AmbientStrength,half3(_AmbientFloor,_AmbientFloor,_AmbientFloor));
                color+=ComicLight(mainLight,normalWS,inputData.viewDirectionWS,albedo,metallic,smoothness);
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                        {
                            Light light=GetAdditionalLight(lightIndex,input.positionWS,shadowMask);
                            color+=ComicLight(light,normalWS,inputData.viewDirectionWS,albedo,metallic,smoothness);
                        }
                    #endif
                    uint lightsCount=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightsCount)
                        Light light=GetAdditionalLight(lightIndex,input.positionWS,shadowMask);
                        color+=ComicLight(light,normalWS,inputData.viewDirectionWS,albedo,metallic,smoothness);
                    LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color+=input.fogAndVertexLight.yzw*albedo;
                #endif
                #if defined(_OCCLUSIONMAP)
                    color*=lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,input.uv).g,_OcclusionStrength);
                #endif
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    color*=ao.indirectAmbientOcclusion;
                #endif
                float hatchCoordinate=(input.uv.x+input.uv.y*.7)*_HatchScale;
                float hatchWidth=max(fwidth(hatchCoordinate),.02);
                half hatch=1-smoothstep(.04,.04+hatchWidth,abs(frac(hatchCoordinate)-.5));
                half shade=1-smoothstep(_ShadowThreshold,_LightThreshold,saturate(dot(normalWS,mainLight.direction))*mainLight.shadowAttenuation);
                color*=1-hatch*shade*_HatchStrength;
                color+=SampleEmission(input.uv,_EmissionColor.rgb,TEXTURE2D_ARGS(_EmissionMap,sampler_EmissionMap));
                color=MixFog(color,input.fogAndVertexLight.x);
                return half4(color,base.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ComicMetaVertex
            #pragma fragment ComicMetaFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct MetaAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 uv1:TEXCOORD1; float2 uv2:TEXCOORD2; half4 color:COLOR; };
            struct MetaVaryings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; half3 normalWS:TEXCOORD2; half4 color:COLOR; };
            MetaVaryings ComicMetaVertex(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS=MetaVertexPosition(input.positionOS,input.uv1,input.uv2,unity_LightmapST,unity_DynamicLightmapST);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                output.color=input.color;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 ComicMetaFragment(MetaVaryings input):SV_Target
            {
                half4 base=ComicBase(input.uv,input.positionWS,normalize(input.normalWS));
                base.rgb*=lerp(half3(1,1,1),input.color.rgb,_VertexTint);
                #if defined(_ALPHATEST_ON)
                    clip(base.a-_Cutoff);
                #endif
                MetaInput meta=(MetaInput)0;
                meta.Albedo=base.rgb;
                meta.Emission=SampleEmission(input.uv,_EmissionColor.rgb,TEXTURE2D_ARGS(_EmissionMap,sampler_EmissionMap));
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
