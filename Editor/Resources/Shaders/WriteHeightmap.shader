Shader "Hidden/GameCraftersGuild/TerrainGen/WriteHeightmap"
{
    Properties
    {
        [NoScaleOffset]_Mask("Mask", 2D) = "white" {}
        [NoScaleOffset]_Data("Data", 2D) = "white" {}
        _HeightRange("HeightRange", Vector) = (0, 1, 0, 0)
        _TerrainWorldHeightRange("TerrainWorldHeightRange", Vector) = (0, 256, 0, 0)
        _SplineMeshBoundsY("SplineMeshBoundsY", Vector) = (0, 1, 0, 0)        
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
            Blend [_SrcMode] [_DstMode], [_AlphaSrcMode] [_AlphaDstMode]
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
                if (mask <= 0.01) discard;
                mask = smoothstep(_Falloff.x, _Falloff.y, mask);

                float heightData = tex2D(_Data, i.uv).x;
                float height = lerp(_HeightRange.x, _HeightRange.y, heightData) * 0.5;
                return float4(height, height, height, mask);
            }
            ENDCG
        }

        Pass // Take position into account
        {
            Cull Off
            BlendOp [_BlendOp]
            Blend [_SrcMode] [_DstMode], [_AlphaSrcMode] [_AlphaDstMode]
            ZTest Never
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float invLerp(float from, float to, float value)
            {
                return (value - from) / (to - from);
            }

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
            float4 _TerrainWorldHeightRange;
            float4 _SplineMeshBoundsY;
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
                float4 sample = tex2D(_Mask, i.uv);
                float mask = sample.x;
                if (mask <= 0.01) discard;
                mask = smoothstep(_Falloff.x, _Falloff.y, mask);

                float heightData = tex2D(_Data, i.uv).x;
                float height = lerp(_HeightRange.x, _HeightRange.y, heightData);

                float splineMeshHeight = sample.b;
                float splineMeshWorldSpaceY = lerp(_SplineMeshBoundsY.x, _SplineMeshBoundsY.y, splineMeshHeight);
                float splineMeshTerrainSpaceY = (splineMeshWorldSpaceY - _TerrainWorldHeightRange.x) / _TerrainWorldHeightRange.y * 0.5f;
                height += splineMeshTerrainSpaceY;
                return float4(height, height, height, mask);
            }
            ENDCG
        }
    }
}
