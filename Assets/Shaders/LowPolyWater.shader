Shader "LowPolyWater/Standard"
{
    Properties
    {
        _AlbedoTex("Albedo", 2D) = "white" {}
        _AlbedoColor("Albedo Color", Color) = (1, 1, 1, 1)
        _SpecularColor("Specular Color", Color) = (0, 0, 0, 0)
       	_Shininess ("Shininess", Float) = 10
       	_FresnelPower ("Fresnel Power", Float) = 5
       	_Reflectivity ("Reflectivity", Range(0.0, 1.0)) = 0.15
       	_Disturbance ("Disturbance", Float) = 10
		[HideInInspector] _ReflectionTex ("", 2D) = "white" {}
    }
	CGINCLUDE
        // SineWave definition
        // _SineWave0: x = amplitude, y = frequency, z = phase
        // _SineWave1: xy = travel direction, z = sharpness
        uniform half3 _SineWave0[8];
        uniform half3 _SineWave1[8];
        uniform int _Waves;
        uniform float _TimeScale;
        // physical model for water waves
        // check: http://http.developer.nvidia.com/GPUGems/gpugems_ch01.html
        float Wave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	half2 D = _SineWave1[i].xy;	// direction
            float sine = sin(dot(D, half2(x, y)) * O + _Time.x * _TimeScale * P);
            return 2.0f * A * pow((sine + 1.0f) / 2.0f, _SineWave1[i].z);
        }
        float dxWave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	half2 D = _SineWave1[i].xy;	// direction
			float term = dot(D, half2(x, y)) * O + _Time.x * _TimeScale * P;
            float power = max(1.0f, _SineWave1[i].z - 1.0f);
            float sinP = pow((sin(term) + 1.0f) / 2.0f, power);
			return _SineWave1[i].z * D.x * O * A * sinP * cos(term);
        }
        float dzWave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	half2 D = _SineWave1[i].xy;	// direction
			float term = dot(D, half2(x, y)) * O + _Time.x * _TimeScale * P;
            float power = max(1.0f, _SineWave1[i].z - 1.0f);
			float sinP = pow((sin(term) + 1.0f) / 2.0f, power);
			return _SineWave1[i].z * D.y * O * A * sinP * cos(term);
        }
        half3 WaveNormal(float x, float y)
        {
        	float dx = 0.0f;
        	float dz = 0.0f;

        	for(int i = 0; i < _Waves; i++)
        	{
        		dx += dxWave(i, x, y);
        		dz += dzWave(i, x, y);
        	}

        	return normalize(half3(-dx, 1.0f, -dz));
        }
        // sum of sines wave transform
        float WaveHeight(float x, float y)
        {
            float height = 0.0;

            for(int i = 0; i < _Waves; i++)
            {
                height += Wave(i, x, y);
            }

            return height;
        }
	ENDCG
    SubShader
    {
        LOD 200
        Pass
        {
        	Tags{ "RenderMode" = "Opaque" "Queue" = "Geometry" "LightMode" = "ForwardBase" }
        	Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap
			// shadow helper functions and macros
			#include "AutoLight.cginc"
			#include "UnityCG.cginc"
			#include "Lighting.cginc"

            struct VS_Input
            {
                half4 vertex : POSITION;
                half3 normal : NORMAL;
                half2 uv : TEXCOORD0;
            };

            struct GS_Output
            {
                half4 pos : SV_POSITION;
                half3 worldPos : WORLDPOSITION;
                half2 uv : TEXCOORD0;
                half3 ambient : AMBIENT;
                half3 diffuse : DIFFUSE;
                half3 specular : SPECULAR;
                half4 _ShadowCoord : TEXCOORD1; // put shadows data into TEXCOORD1
                half4 screenPos : REFLECTION;
            };
            
            struct VS_Output
            {
                half4 pos : SV_POSITION;
                half3 worldPos : WORLDPOSITION;
                half2 uv : TEXCOORD0;
                half4 _ShadowCoord : TEXCOORD1; // put shadows data into TEXCOORD1
                half4 screenPos : REFLECTION;
                half3 waveNormal : WAVENORMAL;
            };

            static const float PI = float(3.14159);

            // properties input
            sampler2D _AlbedoTex;
            sampler2D _ReflectionTex;
            half4 _AlbedoTex_ST;
            half4 _AlbedoColor;
            half4 _SpecularColor;
            float _Shininess;
            float _FresnelPower;
            float _Reflectivity;
            float _Disturbance;

            VS_Output vert(VS_Input v)
            {
                VS_Output o = (VS_Output)0;
                // Water simulation
                v.vertex.xyz += v.normal * WaveHeight(v.vertex.x, v.vertex.z);
                // Space transform
                o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
                o.worldPos = mul(_Object2World, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _AlbedoTex);
                // compute shadows data
        		TRANSFER_SHADOW(o)
        		// reflection
        		o.waveNormal = UnityObjectToWorldNormal(half4(WaveNormal(v.vertex.x, v.vertex.z), 0.0f));
        		half4 dPos = o.pos;
        		dPos.x += _Disturbance * o.waveNormal.x;
        		dPos.z += _Disturbance * o.waveNormal.z;
        		o.screenPos = ComputeScreenPos(dPos);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle VS_Output input[3], inout TriangleStream<GS_Output> OutputStream)
            {
                GS_Output test = (GS_Output)0;
                // average wave normal between the vertices
                half3 waveFx = (input[0].waveNormal + input[2].waveNormal
                                + input[2].waveNormal) / 3.0f;
				// average between the three triangle vertices
				half3 center = (input[0].worldPos + input[1].worldPos 
								+ input[2].worldPos) / 3.0f;
                // shading
                half3 normalDirection = normalize(waveFx);
                half3 lightDirection;

				if (0.0 == _WorldSpaceLightPos0.w) // directional light?
				{
					lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				}
				else // point light or spot
				{
					half3 lightDir = normalize(_WorldSpaceLightPos0.xyz - center);
				}

				// diffuse intensity
				float nDotL = dot(normalDirection, lightDirection);
				half3 ambient = ShadeSH9(half4(normalDirection, 1.0f)) * _AlbedoColor.rgb;
				half3 diffuse = _LightColor0.rgb * _AlbedoColor.rgb * max(0.0f, nDotL);
				half3 specular = half3(0.0f, 0.0f, 0.0f);

				if(nDotL > 0.0f)
				{
					half3 viewDir = normalize(_WorldSpaceCameraPos - center);
					// half vector for blinn specular
					half3 H = normalize(lightDirection + viewDir);
					// specular intensity
					float specIntensity = pow(saturate(dot(normalDirection, H)), _Shininess);
					// schlick's fresnel approximation
					float fresnel = pow(1.0f - max(0.0f, dot(viewDir, H)), _FresnelPower);
					half3 refl2Refr = _SpecularColor.rgb + (1.0f - _SpecularColor.rgb) * fresnel;
					specular = _LightColor0.rgb * refl2Refr * max(0.0f, specIntensity);  
				}

                for (int i = 0; i < 3; i++)
                {
					// pass values to fragment shader
                    test.pos = input[i].pos;
                    test.worldPos = center;
                    test.uv = input[i].uv;
                    test.ambient = ambient;
                    test.specular = specular;
                    test.diffuse = diffuse;
                    test._ShadowCoord = input[i]._ShadowCoord;
                    test.screenPos = input[i].screenPos;
                    OutputStream.Append(test);
                }
            }

            half4 frag(GS_Output i) : SV_Target
            {
            	half4 refl = tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(i.screenPos));
                half4 col = tex2D(_AlbedoTex, i.uv);
                // compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                col.rgb *= i.ambient + (i.diffuse + i.specular) * attenuation;
                col.rgb = lerp(col.rgb, refl.rgb, _Reflectivity);
                return col;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                // Water simulation
                v.vertex.xyz += v.normal * WaveHeight(v.vertex.x, v.vertex.z);
                o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}