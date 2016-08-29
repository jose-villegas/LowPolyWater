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
        uniform float3 _SineWave0[8];
        uniform float3 _SineWave1[8];
        uniform int _Waves;
        uniform float _TimeScale;
        // physical model for water waves
        // check: http://http.developer.nvidia.com/GPUGems/gpugems_ch01.html
        float Wave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	float2 D = _SineWave1[i].xy;	// direction
            float sine = sin(dot(D, float2(x, y)) * O + _Time.x * _TimeScale * P);
            return 2.0f * A * pow((sine + 1.0f) / 2.0f, _SineWave1[i].z);
        }
        float dxWave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	float2 D = _SineWave1[i].xy;	// direction
			float term = dot(D, float2(x, y)) * O + _Time.x * _TimeScale * P;
			float sinP = pow((sin(term) + 1.0f) / 2.0f,  _SineWave1[i].z - 1.0f);
			return _SineWave1[i].z * D.x * O * A * sinP * cos(term);
        }
        float dzWave(int i, float x, float y)
        {
            float A = _SineWave0[i].x; 		// amplitude
			float O = _SineWave0[i].y; 		// frequency
            float P = _SineWave0[i].z; 		// phase
        	float2 D = _SineWave1[i].xy;	// direction
			float term = dot(D, float2(x, y)) * O + _Time.x * _TimeScale * P;
			float sinP = pow((sin(term) + 1.0f) / 2.0f,  _SineWave1[i].z - 1.0f);
			return _SineWave1[i].z * D.y * O * A * sinP * cos(term);
        }
        float3 WaveNormal(float x, float y)
        {
        	float dx = 0.0f;
        	float dz = 0.0f;

        	for(int i = 0; i < _Waves; i++)
        	{
        		dx += dxWave(i, x, y);
        		dz += dzWave(i, x, y);
        	}

        	return normalize(float3(-dx, 1.0f, -dz));
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
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			// shadow helper functions and macros
			#include "AutoLight.cginc"
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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 _ShadowCoord : TEXCOORD1; // put shadows data into TEXCOORD1
                float3 posWorld : WORLDPOSITION;
                float3 normalDir : NORMAL;
                float3 viewDir : VIEWDIRECTION;
				float4 refl : REFLECTION;
                float3 ambient : AMBIENT;
                float3 diffuse : DIFFUSE;
                float3 specular : SPECULAR;
            };

            static const float PI = float(3.14159);

            // properties input
            sampler2D _AlbedoTex;
            sampler2D _ReflectionTex;
            float4 _AlbedoTex_ST;
            fixed4 _AlbedoColor;
            fixed4 _SpecularColor;
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
                o.posWorld = mul(_Object2World, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _AlbedoTex);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.posWorld);
                // compute shadows data
        		TRANSFER_SHADOW(o)
        		// reflection
        		float3 wNormal = UnityObjectToWorldNormal(fixed4(WaveNormal(v.vertex.x, v.vertex.z), 0.0f));
        		float4 dPos = o.pos;
        		dPos.x += _Disturbance * wNormal.x;
        		dPos.z += _Disturbance * wNormal.z;
        		o.refl = ComputeScreenPos(dPos);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle VS_Output input[3], inout TriangleStream<VS_Output> OutputStream)
            {
                VS_Output test = (VS_Output)0;
                // obtain triangle normal
                float3 planeNormal = normalize(cross(input[1].posWorld.xyz -
                                                	 input[0].posWorld.xyz,
                                                	 input[2].posWorld.xyz - 
                                                	 input[0].posWorld.xyz));
                // shading
                float3 normalDirection = UnityObjectToWorldNormal(fixed4(planeNormal, 0.0f));
                float3 lightDirection;

				if (0.0 == _WorldSpaceLightPos0.w) // directional light?
				{
					lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				}

				// diffuse intensity
				float nDotL = dot(normalDirection, lightDirection) * _AlbedoColor.rgb;
				float3 ambient = ShadeSH9(half4(normalDirection, 1.0f));
				float3 diffuse = _LightColor0.rgb * _AlbedoColor.rgb * max(0.0f, nDotL);
				float3 specular = float3(0.0f, 0.0f, 0.0f);

				if(nDotL > 0.0f)
				{
					// average between the three triangle vertices
					float3 center = (input[0].posWorld + input[1].posWorld 
									+ input[2].posWorld) / 3.0f;
					float3 viewDir = normalize(_WorldSpaceCameraPos - center);
					// half vector for blinn specular
					float3 H = normalize(lightDirection + viewDir);
					// specular intensity
					float specIntensity = pow(saturate(dot(normalDirection, H)), _Shininess);
					// schlick's fresnel approximation
					float fresnel = pow(1.0f - max(0.0f, dot(viewDir, H)), _FresnelPower);
					float3 refl2Refr = _SpecularColor.rgb + (1.0f - _SpecularColor.rgb) * fresnel;
					specular = _LightColor0.rgb * refl2Refr * max(0.0f, specIntensity);  
				}


                for (int i = 0; i < 3; i++)
                {
					// pass values to fragment shader
                    test.normalDir = normalDirection;
                    test.pos = input[i].pos;
                    test.uv = input[i].uv;
                    test.viewDir = input[i].viewDir;
                    test.ambient = ambient;
                    test.specular = specular;
                    test.diffuse = diffuse;
                    test._ShadowCoord = input[i]._ShadowCoord;
                    test.refl = input[i].refl;
                    OutputStream.Append(test);
                }
            }
            fixed4 frag(VS_Output i) : SV_Target
            {
            	float4 refl = tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(i.refl));
                float4 col = tex2D(_AlbedoTex, i.uv);
                // compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
                fixed shadow = SHADOW_ATTENUATION(i);
                col.rgb *= i.ambient + (i.diffuse + i.specular) * shadow;
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

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}