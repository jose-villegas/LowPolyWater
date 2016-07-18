Shader "Custom/Phong"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
      	_Shininess ("Shininess", Float) = 10
    }
    SubShader
    {
        Tags{ "RenderMode" = "Opaque" "LightMode" = "ForwardBase" }
        LOD 200

        Pass
        {
            // Cull Front
            // ZWrite Off
            // Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float4 worldPosition : TEXCOORD0;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD1;
            };

            static const float PI = float(3.14159);

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Shininess;
            fixed4 _Color;

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

            inline float3 LightDirection(float3 position)
            {
            	float3 lightDirection;

            	if (0.0 == _WorldSpaceLightPos0.w) // directional light?
	            {
	               lightDirection = normalize(_WorldSpaceLightPos0.xyz);
	            } 
	            else // point or spot light
	            {
	               lightDirection = normalize(_WorldSpaceLightPos0.xyz - mul(_Object2World, position).xyz);
	            }

	            return lightDirection;
            }

            VS_Output vert(VS_Input v)
            {
                VS_Output o = (VS_Output)0;

                o.worldPosition = mul(_Object2World, v.vertex);
                o.normal = normalize(mul(float4(v.normal, 0.0), _World2Object).xyz);
                o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag(VS_Output i) : SV_Target
            {
            	float3 normalDir = normalize(i.normal);
            	float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPosition);
            	float3 lightDir;
            	float att;

            	if(0.0 == _WorldSpaceLightPos0.w)
            	{
            		att = 1.0;
            		lightDir = normalize(_WorldSpaceLightPos0.xyz);
            	}
            	else
            	{
	               float3 vToL = _WorldSpaceLightPos0.xyz - i.worldPosition.xyz;
	               float distance = length(vToL);
	               att = 1.0 / distance; // linear attenuation 
	               lightDir = normalize(vToL);
            	}

            	float3 ambientLight = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb;

            	float3 diffuseReflection = att * _LightColor0.rgb * _Color.rgb * max(0.0, dot(normalDir, lightDir));
            	diffuseReflection += Lighting_Refract(viewDir, normalDir);

            	float3 specularReflection;
	            if (dot(normalDir, lightDir) < 0.0) // light source on the wrong side?
	            {
	               specularReflection = float3(0.0, 0.0, 0.0); // no specular reflection
	            }
	            else // light source on the right side
	            {
	               specularReflection = att * _LightColor0.rgb * _SpecColor.rgb * pow(max(0.0, dot(reflect(-lightDir, normalDir), viewDir)), _Shininess);
	            }

            	return fixed4(diffuseReflection, 0);
            }
            ENDCG
        }
    }
}