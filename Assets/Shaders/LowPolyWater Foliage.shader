// Low poly trees, bushes, grass and flowers that bend in the LowPolyWind field.
// Each plant bends about its base: the sway grows with the square of the height above
// the pivot, so roots stay planted and tops travel furthest. Leaves add a small flutter.
// Canopy occlusion darkens the sky and bounce light inside the crowns, as the Soft Occlusion
// shaders these replace did; optional wrap lighting softens the side away from the sun.
Shader "LowPolyWater/Foliage"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _Wrap("Light Wrap", Range(0, 1)) = 0.0
        _CanopyOcclusion("Canopy Occlusion", Range(0, 1)) = 0.72
        _WindWeight("Wind Bend (m per m^2 of height)", Float) = 0.004
        _Flutter("Leaf Flutter (m)", Float) = 0.015
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf WrapLambert vertex:vert addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0
        #include "LowPolyWind.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        half _Wrap;
        half _CanopyOcclusion;
        float _WindWeight;
        float _Flutter;

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            float3 base = unity_ObjectToWorld._m03_m13_m23;
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float height = max(0.0, world.y - base.y);

            float2 sway = WindSway(base.xz, height) * _WindWeight;
            // Shorten the stem as it bends so the top swings in an arc, not a shear.
            world.y -= dot(sway, sway) / max(2.0 * height, 0.01);
            world.xz += sway;

            float phase = dot(world, float3(1.31, 2.17, 1.73));
            float flutter = sin(LowPolySeconds() * _LowPolyWindMotion.y + phase) * _Flutter
                          * _LowPolyWind.z * saturate(height);
            world += flutter * float3(0.6, 0.35, 0.7);

            v.vertex = mul(unity_WorldToObject, float4(world, 1.0));
        }

        half4 LightingWrapLambert(SurfaceOutput s, half3 viewDir, UnityGI gi)
        {
            half diffuse = saturate((dot(s.Normal, gi.light.dir) + _Wrap) / (1.0 + _Wrap));
            half4 c;
            c.rgb = s.Albedo * gi.light.color * diffuse;     // light colour already carries shadows
        #ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
            c.rgb += s.Albedo * gi.indirect.diffuse * _CanopyOcclusion;
        #endif
            c.a = s.Alpha;
            return c;
        }

        void LightingWrapLambert_GI(SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
        {
            gi = UnityGlobalIllumination(data, 1.0, s.Normal);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/VertexLit"
}
