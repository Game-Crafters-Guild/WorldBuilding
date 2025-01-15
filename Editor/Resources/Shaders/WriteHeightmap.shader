Shader "Hidden/GameCraftersGuild/TerrainGen/WriteHeightmap"
{
    Properties
    {
        [NoScaleOffset]_Mask("Mask", 2D) = "white" {}
        [NoScaleOffset]_Data("Data", 2D) = "white" {}
        _HeightRange("HeightRange", Vector) = (0, 1, 0, 0)
        _Falloff("Falloff", Vector) = (0, 1, 0, 0)
        _BlendOp ("BlendOp", Int) = 0
        _SrcMode ("SrcMode", Int) = 5
	    _DstMode ("DstMode", Int) = 1
        _AlphaSrcMode ("AlphaSrcMode", Int) = 5
	    _AlphaDstMode ("AlphaDstMode", Int) = 1
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
            BlendOp [_BlendOp]
            //BlendOp Sub
            Blend [_SrcMode] [_DstMode], [_AlphaSrcMode] [_AlphaDstMode]
            //Blend One OneMinusDstColor, SrcAlpha DstAlpha
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
                float4 vertex : SV_POSITION;
            };

            sampler2D _Mask;
            sampler2D _Data;
            float4 _HeightRange;
            float4 _Falloff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float mask = tex2D(_Mask, i.uv).x;
                mask = smoothstep(_Falloff.x, _Falloff.y, mask);
                if (mask <= 0.0005) discard;

                float heightData = tex2D(_Data, i.uv).x;
                float height = lerp(_HeightRange.x, _HeightRange.y, heightData);
                return float4(height, height, height, mask);
            }
            ENDCG
        }
    }
}
