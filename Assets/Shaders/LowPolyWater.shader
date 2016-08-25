Shader "Custom/LowPolyWater"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags{ "RenderMode" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct VS_Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct VS_Output
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                fixed4 color : COLOR;
                float3 viewDir : TEXCOORD2;
            };

            static const float PI = float(3.14159);

            // SineWave definition
            // x = amplitude, y = frequency, z = phase, w = direction angle
            uniform float4 _SineWave[8];
            uniform int _Waves;
            uniform float _TimeScale;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float Wave(int i, float x, float y)
            {
                float A = _SineWave[i].x; 										// amplitude
				float O = _SineWave[i].y; 										// frequency
                float P = _SineWave[i].z; 										// phase
                float2 D = float2(cos(_SineWave[i].w), sin(_SineWave[i].w));	// direction
                return A * sin(dot(D, float2(x,y)) * O + _Time.x * _TimeScale * P);
            }

            float WaveHeight(float x, float y)
            {
                float height = 0.0;

                for(int i = 0; i < _Waves; i++)
                {
                    height += Wave(i, x, y);
                }

                return height;
            }

            inline fixed4 Lighting_Reflect(float3 viewDir, float3 normal)
            {
                float4 hdrReflection = 1.0;
                float3 reflectedDir = reflect(viewDir, normal);
                float4 reflection = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectedDir);
                hdrReflection.rgb = DecodeHDR(reflection, unity_SpecCube0_HDR);
                hdrReflection.a = 1.0;

                return hdrReflection;
            }

            inline fixed4 Lighting_Refract(float3 viewDir, float3 normal)
            {
                float4 hdrRefraction = 1.0;
                float3 refractedDir = refract(viewDir, normal, 1.0 / (1.33));
                float4 refraction = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, refractedDir);
                hdrRefraction.rgb = DecodeHDR(refraction, unity_SpecCube0_HDR);
                hdrRefraction.a = 1.0;

                return hdrRefraction;
            }

            VS_Output vert(VS_Input v)
            {
                VS_Output o = (VS_Output)0;
                // Water simulation
                v.vertex.y += WaveHeight(v.vertex.x, v.vertex.z);
                // v.vertex.y = _Amplitude[0];
                // Space transform
                o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = v.normal;
                o.worldPosition = mul(_Object2World, v.vertex).xyz;
                // view dir
                o.viewDir = o.worldPosition - _WorldSpaceCameraPos;

                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle VS_Output input[3], inout TriangleStream<VS_Output> OutputStream)
            {
                VS_Output test = (VS_Output)0;
                float3 normal = normalize(cross(input[1].worldPosition.xyz -
                                                input[0].worldPosition.xyz,
                                                input[2].worldPosition.xyz - 
                                                input[0].worldPosition.xyz));
                // shading
                float3 normalDirection = normalize(mul(float4(normal, 0.0), _Object2World).xyz);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float nDotL = dot(normalDirection, normalize(float3(1, 1, 0)));
                fixed4 color = _LightColor0 * fixed4(_Color.rgb * nDotL, _Color.a);

                for (int i = 0; i < 3; i++)
                {
                    test.normal = normal;
                    test.vertex = input[i].vertex;
                    test.uv = input[i].uv;
                    test.color = color;
                    test.viewDir = input[i].viewDir;
                    OutputStream.Append(test);
                }
            }

            fixed4 frag(VS_Output i) : SV_Target
            {
                // obtain surface color
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                // col *= Lighting_Reflect(i.viewDir, i.normal);
                // col *= Lighting_Refract(i.viewDir, i.normal);
                return col;
            }
            ENDCG
        }
    }
    // Fallback "Diffuse"
}