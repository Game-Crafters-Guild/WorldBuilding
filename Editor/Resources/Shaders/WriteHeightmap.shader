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
                mask = saturate((mask - _Falloff.x) / (_Falloff.y - _Falloff.x));

                float heightData = tex2D(_Data, i.uv).x;
                float height = lerp(_HeightRange.x, _HeightRange.y, heightData) * 0.5;
                float4 packedHeight = PackHeightmap(height);
                return float4(packedHeight.x, packedHeight.y, packedHeight.z, mask);
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
                // Sample with higher precision
                float4 maskSample = tex2D(_Mask, i.uv);
                float mask = maskSample.r; // Mask intensity is in red channel
                
                // Early exit if mask is too weak
                if (mask <= 0.005) discard;
                
                // Apply falloff
                mask = saturate((mask - _Falloff.x) / (_Falloff.y - _Falloff.x));
                
                // The blue channel contains the normalized height from the spline
                float encodedHeight = maskSample.b;
                
                // Convert from normalized spline height (0-1) to world space
                float splineWorldHeight = lerp(_SplineMeshBoundsY.x, _SplineMeshBoundsY.y, encodedHeight);
                
                // Convert from world space to terrain space (0-0.5)
                float terrainRange = max(0.001, _TerrainWorldHeightRange.y - _TerrainWorldHeightRange.x);
                float terrainHeight = (splineWorldHeight - _TerrainWorldHeightRange.x) / terrainRange * 0.5;
                
                // Pack height for output (no blending with base height)
                float4 packedHeight = PackHeightmap(terrainHeight);
                
                return float4(packedHeight.rgb, mask);
            }
            ENDCG
        }
    }
}