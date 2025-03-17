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
        _MaskRange("MaskRange", Vector) = (0, 1, 0, 0)
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
        
        CGINCLUDE
            // Falloff functions
            float ApplyFalloff(float mask, float minFalloff, float maxFalloff, float falloffType, float2 maskRange, float innerFalloff)
            {
                // First check if the mask is within the valid range
                float maskValue = mask;
                float maskMin = maskRange.x;
                float maskMax = maskRange.y;
                
                // Check if the mask is outside the valid range (excluding the falloff regions)
                if (maskValue < maskMin || maskValue > maskMax)
                {
                    return 0.0;
                }
                
                // Apply inner falloff (for inner part of crater - higher values)
                float innerWeight = 1.0;
                if (innerFalloff > 0.0 && maskValue > maskMax - innerFalloff)
                {
                    // Apply a smooth transition at the inner boundary (higher values)
                    innerWeight = smoothstep(maskMax + 0.001, maskMax - innerFalloff, maskValue);
                }
                
                // Remap the mask value from maskRange to 0-1 range for falloff calculation
                if (maskMax > maskMin)
                {
                    maskValue = (maskValue - maskMin) / (maskMax - maskMin);
                }
                else
                {
                    maskValue = 0.0;
                }
                
                // FalloffType enum: Linear = 0, Smoothstep = 1, EaseIn = 2, EaseOut = 3, SmoothEaseInOut = 4
                int type = round(falloffType);
                
                // Linear
                float result = saturate((maskValue - minFalloff) / (maxFalloff - minFalloff));
                
                if (type == 1) // Smoothstep
                {
                    result = smoothstep(minFalloff, maxFalloff, maskValue);
                }
                else if (type == 2) // EaseIn - quadratic
                {
                    float t = saturate((maskValue - minFalloff) / (maxFalloff - minFalloff));
                    result = t * t;
                }
                else if (type == 3) // EaseOut - inverse quadratic
                {
                    float t = saturate((maskValue - minFalloff) / (maxFalloff - minFalloff));
                    result = t * (2 - t);
                }
                else if (type == 4) // SmoothEaseInOut - cubic
                {
                    float t = saturate((maskValue - minFalloff) / (maxFalloff - minFalloff));
                    result = t * t * (3 - 2 * t);
                }
                
                // Apply the inner weight for a smooth transition at the boundary
                return result * innerWeight;
            }
        ENDCG

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
            float4 _MaskRange;

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
                mask = ApplyFalloff(mask, _Falloff.x, _Falloff.y, _Falloff.z, _MaskRange.xy, _MaskRange.z);

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
            float4 _MaskRange;

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
                mask = ApplyFalloff(mask, _Falloff.x, _Falloff.y, _Falloff.z, _MaskRange.xy, _MaskRange.z);
                
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