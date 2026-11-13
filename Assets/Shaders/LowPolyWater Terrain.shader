// Standard terrain whose baked shadows sway with the foliage.
// Plants bend in the LowPolyWind field, but their shadows are baked into the shadowmask.
// Each vertex shifts its lightmap lookup by the sway of the canopy that shades it, found
// by stepping toward the sun up to a typical caster height, so the baked shadows move
// with the trees. The gust field is smooth, so per-vertex warping is enough.
Shader "LowPolyWater/Terrain"
{
    Properties
    {
        [Header(Baked Shadow Sway)]
        _ShadowSway("Sway at Caster Height (m per unit wind)", Float) = 0.1
        _ShadowCasterHeight("Typical Caster Height (m)", Float) = 5
        [HideInInspector] _TerrainSize("Terrain Size", Vector) = (100, 20, 100, 0)

        // Set by the terrain.
        [HideInInspector] [ToggleUI] _EnableHeightBlend("EnableHeightBlend", Float) = 0.0
        [HideInInspector] _HeightTransition("Height Transition", Range(0, 1.0)) = 0.0
        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}
        [HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat3("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Splat2("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}
        [HideInInspector] [Gamma] _Metallic0("Metallic 0", Range(0.0, 1.0)) = 0.0
        [HideInInspector] [Gamma] _Metallic1("Metallic 1", Range(0.0, 1.0)) = 0.0
        [HideInInspector] [Gamma] _Metallic2("Metallic 2", Range(0.0, 1.0)) = 0.0
        [HideInInspector] [Gamma] _Metallic3("Metallic 3", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Smoothness0("Smoothness 0", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Smoothness1("Smoothness 1", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Smoothness2("Smoothness 2", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _Smoothness3("Smoothness 3", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _Color("Main Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "TerrainCompatible" = "True" }

        CGPROGRAM
        #pragma surface surf Standard vertex:WarpedSplatmapVert finalcolor:SplatmapFinalColor addshadow fullforwardshadows
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
        #pragma multi_compile_fog
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        #pragma multi_compile_local_fragment __ _ALPHATEST_ON
        #pragma multi_compile_local __ _NORMALMAP

        #define TERRAIN_STANDARD_SHADER
        #define TERRAIN_INSTANCED_PERPIXEL_NORMAL
        #define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
        #include "TerrainSplatmapCommon.cginc"
        #include "LowPolyWind.cginc"

        half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
        half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
        float _ShadowSway;
        float _ShadowCasterHeight;
        float4 _TerrainSize;

        void WarpedSplatmapVert(inout appdata_full v, out Input data)
        {
            SplatmapVert(v, data);

        #if !defined(UNITY_PASS_META)   // the lightmapper rasterises by texcoord1: never warp it
            // The canopy that shades this point sits up the ray toward the sun.
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 toSun = normalize(_WorldSpaceLightPos0.xyz);
            float2 canopy = world.xz + toSun.xz / max(toSun.y, 0.2) * _ShadowCasterHeight;
            float2 sway = _LowPolyWind.xy * (_LowPolyWind.z * (0.35 + WindGust(canopy))) * _ShadowSway;

            // A canopy that moved by `sway` now shades the point `sway` downwind of where
            // it was baked, so look the shadow up that far upwind.
            v.texcoord1.xy -= sway / _TerrainSize.xz;
        #endif
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half4 splat_control;
            half weight;
            fixed4 mixedDiffuse;
            half4 defaultSmoothness = half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);
            SplatmapMix(IN, defaultSmoothness, splat_control, weight, mixedDiffuse, o.Normal);
            o.Albedo = mixedDiffuse.rgb;
            o.Alpha = weight;
            o.Smoothness = mixedDiffuse.a;
            o.Metallic = dot(splat_control, half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3));
        }
        ENDCG

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
        UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
    }

    Dependency "AddPassShader" = "Hidden/TerrainEngine/Splatmap/Standard-AddPass"
    Dependency "BaseMapShader" = "Hidden/TerrainEngine/Splatmap/Standard-Base"
    Dependency "BaseMapGenShader" = "Hidden/TerrainEngine/Splatmap/Standard-BaseGen"

    Fallback "Nature/Terrain/Diffuse"
}
