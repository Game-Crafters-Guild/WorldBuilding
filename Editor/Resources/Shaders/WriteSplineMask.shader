Shader "Hidden/GameCraftersGuild/TerrainGen/WriteSplineMask"
{
    Properties
    {
        _LocalBoundsMinY("LocalBoundsMinY", Float) = 256
        _LocalBoundsMaxY("LocalBoundsMaxY", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
        }
        LOD 100

        Pass
        {
            Cull Off
            BlendOp Max
            Blend One One, One One
            ZTest Never
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 positionOS : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.positionOS = v.vertex;
                return o;
            }

            void Unity_InverseLerp_float(float A, float B, float T, out float Out)
            {
                Out = (T - A)/(B - A);
            }

            float _LocalBoundsMinY;
            float _LocalBoundsMaxY;
            float4 frag (v2f i) : SV_Target
            {
                float mask = min(1.0 - abs(i.uv.x), i.uv.y);

                // Encode Y position in bounds space.
                float y = 0.0;
                Unity_InverseLerp_float(_LocalBoundsMinY, _LocalBoundsMaxY, i.positionOS.y, y);
                float4 result = float4(mask, mask, y, mask);
                return result;
            }
            ENDCG
        }
    }
}
