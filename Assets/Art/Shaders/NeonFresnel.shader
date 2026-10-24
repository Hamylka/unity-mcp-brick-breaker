// Unlit neon "glass" for small glowing objects (the ball, bonus crystals).
// The color blends from Core (facing the camera) to Rim (grazing angles), so a sphere reads as a hot core inside
// a glowing shell and a flat-shaded gem gets bright faceted edges. An optional hotspot brightens the very center.
// Colors are HDR: values above 1 feed the bloom. Per-object colors can be set with a MaterialPropertyBlock.
Shader "BrickBreaker/Neon Fresnel"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _RimColor ("Rim Color", Color) = (2, 0.8, 0.2, 1)
        _RimPower ("Rim Power", Range(0.3, 8)) = 2
        _Hotspot ("Center Hotspot", Range(0, 4)) = 0
        _HotspotPower ("Hotspot Tightness", Range(1, 32)) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CoreColor;
            half4 _RimColor;
            half _RimPower;
            half _Hotspot;
            half _HotspotPower;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "NeonFresnel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(position.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _RimPower);
                half hotspot = pow(facing, _HotspotPower) * _Hotspot;
                half3 color = lerp(_CoreColor.rgb, _RimColor.rgb, rim) + _CoreColor.rgb * hotspot;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Writes depth so post effects and the depth prepass see the object.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}
