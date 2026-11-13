// Low poly water for the built-in render pipeline.
// - Waves are displaced in the vertex stage (LowPolyWaves.cginc).
// - Facets come from the screen-space derivatives of the displaced position, so the
//   flat look needs no geometry shader and runs on every graphics API.
// - With Depth Volume on, the water reads the camera depth texture to tint, absorb
//   and refract what lies under it, and draws a foam band where it meets the shore.
Shader "LowPolyWater/Transparent"
{
    Properties
    {
        _AlbedoTex("Albedo", 2D) = "white" {}
        _AlbedoColor("Albedo Color", Color) = (1, 1, 1, 1)
        _SpecularColor("Specular Color", Color) = (0, 0, 0, 0)
        _Shininess("Shininess", Float) = 10
        _FresnelPower("Fresnel Power", Float) = 5
        _Reflectivity("Reflectivity", Range(0.0, 1.0)) = 0.15
        _Disturbance("Reflection Disturbance", Float) = 10

        [Header(Volume)]
        [Toggle(_WATER_VOLUME)] _Volume("Depth Volume", Float) = 1
        _DeepColor("Deep Color", Color) = (0.06, 0.28, 0.42, 1)
        _Absorption("Absorption per Unit (RGB)", Vector) = (0.35, 0.09, 0.06, 0)
        _Refraction("Refraction", Range(0.0, 0.2)) = 0.03

        [Header(Shore Foam)]
        _FoamColor("Foam Color", Color) = (0.92, 0.98, 1, 0.75)
        _FoamDistance("Foam Depth", Float) = 0.25
        _FoamBands("Foam Bands", Range(1, 4)) = 2

        [HideInInspector] _ReflectionTex("", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        // Everything opaque drawn so far, shared by all water surfaces this frame.
        GrabPass { "_WaterBackground" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_local _ _WAVES_GERSTNER
            #pragma shader_feature_local _WATER_VOLUME

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "LowPolyWaves.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 screenPos : TEXCOORD2;   // w = clip w, used for both grab and depth
                float eyeDepth : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            sampler2D _AlbedoTex;
            float4 _AlbedoTex_ST;
            sampler2D _ReflectionTex;
            half4 _AlbedoColor;
            half4 _SpecularColor;
            float _Shininess;
            float _FresnelPower;
            float _Reflectivity;
            float _Disturbance;

            sampler2D _WaterBackground;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            half4 _DeepColor;
            float4 _Absorption;
            float _Refraction;
            half4 _FoamColor;
            float _FoamDistance;
            float _FoamBands;

            v2f vert(appdata v)
            {
                v2f o;
                float4 local = float4(DisplaceWater(v.vertex.xyz), 1.0);
                o.pos = UnityObjectToClipPos(local);
                o.worldPos = mul(unity_ObjectToWorld, local).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _AlbedoTex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityObjectToViewPos(local).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 toCamera = _WorldSpaceCameraPos - i.worldPos;
                float viewDistance = length(toCamera);
                half3 V = toCamera / viewDistance;

                // Face normal of the displaced triangle: the low poly facet.
                half3 N = normalize(cross(ddy(i.worldPos), ddx(i.worldPos)));
                N = dot(N, V) < 0 ? -N : N;

                half3 L = _WorldSpaceLightPos0.w == 0
                    ? normalize(_WorldSpaceLightPos0.xyz)
                    : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                half nDotL = saturate(dot(N, L));

                half3 ambient = ShadeSH9(half4(N, 1.0));
                half3 lighting = ambient + _LightColor0.rgb * nDotL;

                // Blinn-Phong highlight with Schlick's Fresnel on the specular colour.
                half3 H = normalize(L + V);
                half fresnel = pow(1.0 - saturate(dot(V, H)), _FresnelPower);
                half3 specular = _LightColor0.rgb * (_SpecularColor.rgb + (1.0 - _SpecularColor.rgb) * fresnel)
                               * pow(saturate(dot(N, H)), _Shininess) * (nDotL > 0);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 reflUV = screenUV;
                reflUV.x += 0.5 * _Disturbance * N.x / i.screenPos.w;
                half3 reflection = tex2D(_ReflectionTex, reflUV).rgb;

                half3 albedo = tex2D(_AlbedoTex, i.uv).rgb * _AlbedoColor.rgb;
                half4 col;

            #if defined(_WATER_VOLUME)
                // Length of the view ray per unit of eye depth, to turn depth gaps into distances.
                float rayScale = viewDistance / i.eyeDepth;

                // Refract along the facet normal; keep the straight sample if the bent one
                // lands on something in front of the water.
                float2 refractUV = screenUV + N.xz * _Refraction;
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, refractUV));
                if (sceneDepth < i.eyeDepth)
                {
                    refractUV = screenUV;
                    sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV));
                }
                float thickness = max(0.0, sceneDepth - i.eyeDepth) * rayScale;

                // Beer-Lambert: red goes first, so shallows are clear and depths turn blue.
                half3 transmittance = exp(-_Absorption.rgb * thickness);
                half3 below = tex2D(_WaterBackground, refractUV).rgb;
                half3 body = _DeepColor.rgb * lighting;
                half3 tint = lerp(half3(1, 1, 1), albedo, saturate(thickness));
                col.rgb = below * transmittance * tint + body * (1.0 - transmittance);
                col.rgb = lerp(col.rgb, reflection, _Reflectivity);

                // Vertical water depth at this pixel, banded so the foam stays faceted.
                float3 scenePos = _WorldSpaceCameraPos - V * (sceneDepth * rayScale);
                float waterDepth = max(0.0, i.worldPos.y - scenePos.y);
                half foam = saturate(1.0 - waterDepth / _FoamDistance);
                foam = ceil(foam * _FoamBands) / _FoamBands;
                col.rgb = lerp(col.rgb, _FoamColor.rgb * lighting, foam * _FoamColor.a);
                col.a = 1.0;
            #else
                col.rgb = albedo * lighting;
                col.rgb = lerp(col.rgb, reflection, _Reflectivity);
                col.a = _AlbedoColor.a;
            #endif

                col.rgb += specular;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
