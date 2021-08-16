Shader "Unlit/MediaCodecTest"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

    SubShader{

        //Vertical Blur
        pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "unitycg.cginc"

            sampler2D _MainTex;

            float4 _MainTex_ST;

            struct v2f {
                float4 pos:SV_POSITION;
                //float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
            };

            fixed4 resampleTexture(sampler2D tex, float2 coord, float4 rect) {
                float2 offset = float2(coord.x - rect.x, coord.y - rect.z);
                float2 scale = 1 / float2(abs(rect.x - rect.y), abs(rect.z - rect.w));
                return tex2D(tex, offset * scale);
            }

            // x:left y:right z:bottom w:top
            float4 computeRect(float _FoveaCoordinateX, float _FoveaCoordinateY, float Eccentricity) {
            return clamp(float4(_FoveaCoordinateX - Eccentricity, _FoveaCoordinateX + Eccentricity,
                                _FoveaCoordinateY - Eccentricity, _FoveaCoordinateY + Eccentricity),
                                0, 1);
            }

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f IN) :SV_Target
            {
                return tex2D(_MainTex, IN.uv);
            }


            ENDCG
        }
    }

}
