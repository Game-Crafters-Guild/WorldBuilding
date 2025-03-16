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
            float4 _TerrainWorldHeightRange;
            float4 _SplineMeshBoundsY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Higher precision sampling
                float4 mask = tex2D(_Mask, i.uv);
                float maskIntensity = mask.r;
                
                // Early exit if mask is too weak
                if (maskIntensity <= 0.005)
                    return tex2D(_Data, i.uv);
                
                // Apply falloff to make path blend nicely with surroundings
                float falloff = smoothstep(_Falloff.x, _Falloff.y, maskIntensity);
                
                // CRITICAL FIX: Extract height from the mask texture
                // If available, use direct height value from green channel
                float rawHeight = mask.g;
                float encodedHeight = mask.b;
                
                // Get both the base height and the spline height
                float baseHeight = tex2D(_Data, i.uv).r;
                
                // Note that _SplineMeshBoundsY is defined in the properties section
                float worldMinY = _SplineMeshBoundsY.x; 
                float worldMaxY = _SplineMeshBoundsY.y;
                
                // Calculate final world space height based on encoding
                float worldSplineHeight;
                
                // If direct height (green channel) is available and non-zero, use it
                if (rawHeight > 0.001) {
                    // Use the direct height from the green channel
                    worldSplineHeight = rawHeight;
                } else {
                    // Fall back to normalized height from blue channel
                    worldSplineHeight = lerp(worldMinY, worldMaxY, encodedHeight);
                }
                
                // Convert from world space to terrain space (0-1 range for heightmap)
                float terrainMinY = _TerrainWorldHeightRange.x;
                float terrainMaxY = _TerrainWorldHeightRange.y;
                float terrainRange = terrainMaxY - terrainMinY;
                
                // Normalize to terrain space (0-1)
                float normalizedTerrainHeight = (worldSplineHeight - terrainMinY) / terrainRange;
                
                // Clamp to ensure we stay within valid range
                normalizedTerrainHeight = saturate(normalizedTerrainHeight);
                
                // CRITICAL: For paths, we want to REPLACE the height, not blend it
                // This ensures the exact height is used without being affected by existing terrain
                float finalHeight = lerp(baseHeight, normalizedTerrainHeight, falloff);
                
                // Return the calculated height
                return finalHeight;
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
                float falloffRange = max(0.01, _Falloff.y - _Falloff.x);
                mask = saturate(smoothstep(_Falloff.x, _Falloff.y, mask));
                
                // CRITICAL: The blue channel contains the normalized height from the spline
                float encodedHeight = maskSample.b;
                
                // Convert from normalized spline height (0-1) to world space
                float splineWorldHeight = lerp(_SplineMeshBoundsY.x, _SplineMeshBoundsY.y, encodedHeight);
                
                // Convert from world space to terrain space (0-0.5)
                float terrainRange = max(0.001, _TerrainWorldHeightRange.y - _TerrainWorldHeightRange.x);
                float terrainHeight = (splineWorldHeight - _TerrainWorldHeightRange.x) / terrainRange * 0.5;
                
                // Ensure valid height
                terrainHeight = clamp(terrainHeight, 0.0, 0.5);
                
                // Pack height for output (no blending with base height)
                float4 packedHeight = PackHeightmap(terrainHeight);
                
                return float4(packedHeight.rgb, mask);
            }
            ENDCG
        }
    }
}
