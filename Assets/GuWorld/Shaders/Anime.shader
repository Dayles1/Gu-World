// GU World's people: a dark-anime look for the bodies, hair and clothes.
//
// - Light falls in two flat tones (cel shading): lit, and a tinted shade
//   with a narrow soft edge between them; shadows cast on the body are
//   stepped the same way.
// - The ambient light of the sky (day, dusk, night) fills both tones, so the
//   figures follow the time of day like everything else.
// - A thin rim of light along the silhouette, stronger on the lit side.
// - Lamps and the moon (additional lights) add their own stepped light.
// - An ink outline (the mesh drawn again, a little larger, from behind) whose
//   width is set in screen pixels and thins out with distance; its colour is
//   a dark shade of the surface under it, not flat black.
//
// Made by Body/Anime.cs from each body material at runtime; the texture,
// colour, tiling and culling are copied from the material it replaces.
Shader "GuWorld/Anime"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0

        [Header(Shading)]
        _ShadeColor ("Shade tint", Color) = (0.62, 0.6, 0.75, 1)
        _ShadeStep ("Where the shade begins", Range(-1, 1)) = 0.0
        _ShadeSoftness ("Shade edge softness", Range(0.001, 0.5)) = 0.05
        _Ambient ("Ambient strength", Range(0, 2)) = 1

        [Header(Rim light)]
        _RimColor ("Rim colour", Color) = (1, 0.95, 0.9, 1)
        _RimStrength ("Rim strength", Range(0, 2)) = 0.35
        _RimWidth ("Rim width", Range(0, 1)) = 0.3

        [Header(Outline)]
        _OutlineWidth ("Outline width (pixels)", Range(0, 6)) = 1.4
        _OutlineColor ("Outline colour", Color) = (0.07, 0.05, 0.06, 1)
        _OutlineMix ("Outline takes the surface colour", Range(0, 1)) = 0.35
        _OutlineNear ("Full width nearer than (m)", Float) = 3

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // One buffer for every pass (keeps the SRP Batcher working).
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _AlphaClip;
            half4 _ShadeColor;
            half _ShadeStep;
            half _ShadeSoftness;
            half _Ambient;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            float _OutlineWidth;
            half4 _OutlineColor;
            half _OutlineMix;
            float _OutlineNear;
            half _Cull;
        CBUFFER_END

        half4 SampleSurface(float2 uv)
        {
            half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif
            return albedo;
        }
        ENDHLSL

        // The surface, cel-shaded.
        Pass
        {
            Name "AnimeForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // How lit a surface is by one light, in two tones with a soft edge.
            half Step(half nDotL)
            {
                return smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, nDotL);
            }

            // A cast shadow, stepped like the shading.
            half StepShadow(half attenuation)
            {
                return smoothstep(0.35, 0.65, attenuation);
            }

            half3 OtherLight(Light light, float3 normalWS, half3 albedo)
            {
                half nDotL = dot(normalWS, light.direction);
                half amount = Step(nDotL) * light.distanceAttenuation * StepShadow(light.shadowAttenuation);
                return albedo * light.color * amount;
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleSurface(input.uv);

                // Drawn from both sides: the inside of a sleeve faces the other way.
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 positionWS = input.positionWS;
                half3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // The sun (or the moon, when it is the main light).
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light sun = GetMainLight(shadowCoord);
                half nDotL = dot(normalWS, sun.direction);
                half lit = Step(nDotL) * StepShadow(sun.shadowAttenuation);
                half3 mainColour = sun.color * sun.distanceAttenuation;

                // Lit side: the full light, a little softer where it grazes.
                half3 direct = albedo.rgb * mainColour * lit * (0.8 + 0.2 * saturate(nDotL));

                // The sky's light, taken half from straight up so the tones stay flat.
                half3 ambient = SampleSH(normalize(lerp(normalWS, float3(0, 1, 0), 0.5))) * _Ambient;
                half3 fill = albedo.rgb * ambient;

                // The shade is the surface's colour tinted, not just darker.
                half3 tint = lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                half3 colour = (direct + fill) * tint;

                // Lamps and a second sky light.
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            colour += OtherLight(GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1)), normalWS, albedo.rgb);
                        }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        colour += OtherLight(GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1)), normalWS, albedo.rgb);
                    LIGHT_LOOP_END
                #endif

                // A thin rim of light on the outline, mostly where the light comes from.
                half fresnel = 1.0 - saturate(dot(normalWS, viewWS));
                half rim = smoothstep(1.0 - _RimWidth, 1.0 - _RimWidth + 0.08, fresnel);
                half rimSide = saturate(nDotL * 0.6 + 0.4);
                colour += albedo.rgb * rim * rimSide * _RimStrength * _RimColor.rgb * (mainColour * StepShadow(sun.shadowAttenuation) + ambient);

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        // The ink outline: the mesh pushed out along its normals, seen from behind.
        Pass
        {
            Name "AnimeOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // The normal's direction on screen.
                float2 direction = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float length2 = dot(direction, direction);
                direction = length2 > 1e-8 ? direction * rsqrt(length2) : float2(0, 0);

                // A width in pixels, full near the camera and thinner further off.
                float viewDepth = max(positionCS.w, 1e-4);
                float pixels = _OutlineWidth * saturate(_OutlineNear / viewDepth);
                positionCS.xy += direction * pixels * 2.0 / _ScreenParams.xy * positionCS.w;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // No outline at all (the eyes, the brows).
                clip(_OutlineWidth - 0.01);

                half4 albedo = SampleSurface(input.uv);
                half3 colour = lerp(_OutlineColor.rgb, albedo.rgb * 0.3, _OutlineMix);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SampleSurface(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SampleSurface(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    // Anything the pipeline can't run this on gets URP's own simple shading.
    FallBack "Universal Render Pipeline/Simple Lit"
}
