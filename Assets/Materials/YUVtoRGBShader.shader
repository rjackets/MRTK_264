Shader "Custom/YUVtoRGBHololens"
{
    Properties
    {
        _YTex ("Y Texture", 2D) = "white" {}
        _UVTex ("UV Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Enable instancing support for stereo rendering
            #pragma multi_compile_instancing

            sampler2D _YTex;
            sampler2D _UVTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Stereo rendering adjustment
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Flip the texture vertically
                float2 flippedUV = float2(i.uv.x, 1.0 - i.uv.y);

                float y = tex2D(_YTex, flippedUV).r;
                float2 uv = tex2D(_UVTex, flippedUV).rg - 0.5;

                float3 rgb = float3(y + 1.403 * uv.y,
                                    y - 0.344 * uv.x - 0.714 * uv.y,
                                    y + 1.770 * uv.x);

                return float4(rgb, 1.0);
            }
            ENDCG
        }
    }
}
