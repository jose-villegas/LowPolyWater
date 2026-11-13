// Skybox that continues the water past the edge of the water mesh, out to the horizon.
// - Above the horizon it draws the sky from a cubemap (captured from the procedural sky
//   by Tools > Low Poly Water > Bake Ocean Skybox).
// - Below it, every view ray is intersected with an infinite plane at the water level, so
//   the ocean lines up in perspective with the water mesh. The plane gets low poly facets
//   that fade out before they would alias, the water mesh's deep colour near the mesh,
//   and a Fresnel reflection of the sky further out that meets the sky at the horizon.
Shader "LowPolyWater/Skybox Ocean"
{
    Properties
    {
        [NoScaleOffset] _Sky("Sky Cubemap", Cube) = "grey" {}

        [Header(Ocean)]
        _WaterLevel("Water Level (world Y)", Float) = 6.5
        _WaterColor("Water Color (lit)", Color) = (0.08, 0.36, 0.55, 1)
        _Reflectivity("Reflectivity near the Mesh", Range(0, 1)) = 0.25
        _Center("Water Mesh Center (world XZ)", Vector) = (50, 0, 50, 0)
        _FresnelStart("Fresnel Blend Start (m from center)", Float) = 70
        _FresnelEnd("Fresnel Blend End (m from center)", Float) = 350

        [Header(Facets)]
        _FacetSize("Facet Size (m)", Float) = 2.5
        _FacetTilt("Facet Tilt", Range(0, 0.5)) = 0.12
        _FacetSpeed("Facet Speed", Float) = 1.5
        _HorizonSoftness("Horizon Softness", Range(0.0001, 0.05)) = 0.002
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LowPolyTime.cginc"

            samplerCUBE _Sky;
            float _WaterLevel;
            half4 _WaterColor;
            float _Reflectivity;
            float4 _Center;
            float _FresnelStart;
            float _FresnelEnd;
            float _FacetSize;
            float _FacetTilt;
            float _FacetSpeed;
            float _HorizonSoftness;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Normal of the triangular facet containing p; each facet rocks on its own phase.
            float3 FacetNormal(float2 p, float footprint)
            {
                float2 q = p / _FacetSize;
                float2 cell = floor(q);
                float2 f = q - cell;
                float2 h = Hash22(cell * 2.0 + step(f.y, f.x));
                float time = LowPolySeconds();
                float angle = h.x * 6.2831853;
                float tilt = _FacetTilt * sin(time * _FacetSpeed + h.y * 6.2831853);
                // Once a pixel covers a whole facet, flatten it instead of shimmering.
                tilt *= saturate(1.0 - footprint / _FacetSize);
                return normalize(float3(cos(angle) * tilt, 1.0, sin(angle) * tilt));
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 sky = texCUBElod(_Sky, float4(d, 0)).rgb;

                // Where the view ray meets the water plane.
                float height = max(_WorldSpaceCameraPos.y - _WaterLevel, 0.05);
                float distance = height / max(-d.y, 1e-4);
                float2 p = _WorldSpaceCameraPos.xz + d.xz * distance;
                float footprint = max(fwidth(p.x), fwidth(p.y));

                float3 n = FacetNormal(p, footprint);
                float3 r = reflect(d, n);
                r.y = abs(r.y);
                half3 reflection = texCUBElod(_Sky, float4(r, 0)).rgb;

                // Near the mesh: the mesh's own mix. Far out: physical Fresnel, which reaches
                // full reflection at the horizon so the sea meets the sky without a seam.
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(-d, n)), 5.0);
                float far = saturate((length(p - _Center.xz) - _FresnelStart) / max(_FresnelEnd - _FresnelStart, 1e-3));
                float k = lerp(_Reflectivity, max(_Reflectivity, fresnel), far);
                half3 ocean = lerp(_WaterColor.rgb, reflection, k);

                float below = saturate(-d.y / _HorizonSoftness);
                return half4(lerp(sky, ocean, below), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
