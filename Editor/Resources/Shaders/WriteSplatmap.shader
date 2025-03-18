Shader "Hidden/GameCraftersGuild/TerrainGen/WriteSplatmap"
{
    Properties
    {
        [NoScaleOffset]_Mask("Mask", 2D) = "white" {}
        [NoScaleOffset]_Splatmap("Splatmap", 2D) = "black" {}
        [NoScaleOffset]_NormalMap("Normal Map", 2D) = "bump" {}
        _Intensity("Intensity", Color) = (0, 0, 0, 0)
        _Falloff("Falloff", Vector) = (0, 1, 0, 0)
        _MaskRange("MaskRange", Vector) = (0, 1, 0, 0)
        _SlopeRange("SlopeRange", Vector) = (0, 1, 0, 0)
        _TerrainUVParams("TerrainUVParams (CenterX, CenterY, SizeX, SizeY)", Vector) = (0.5, 0.5, 1, 1)
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
                
                // Ensure there's a minimum difference between min and max falloff to prevent precision issues
                const float MIN_FALLOFF_RANGE = 0.01;
                float falloffRange = max(maxFalloff - minFalloff, MIN_FALLOFF_RANGE);
                float adjustedMaxFalloff = minFalloff + falloffRange;
                
                // FalloffType enum: Linear = 0, Smoothstep = 1, EaseIn = 2, EaseOut = 3, SmoothEaseInOut = 4
                int type = round(falloffType);
                
                // Linear
                float result = saturate((maskValue - minFalloff) / falloffRange);
                
                if (type == 1) // Smoothstep
                {
                    result = smoothstep(minFalloff, adjustedMaxFalloff, maskValue);
                }
                else if (type == 2) // EaseIn - quadratic
                {
                    float t = saturate((maskValue - minFalloff) / falloffRange);
                    result = t * t;
                }
                else if (type == 3) // EaseOut - inverse quadratic
                {
                    float t = saturate((maskValue - minFalloff) / falloffRange);
                    result = t * (2 - t);
                }
                else if (type == 4) // SmoothEaseInOut - cubic
                {
                    float t = saturate((maskValue - minFalloff) / falloffRange);
                    result = t * t * (3 - 2 * t);
                }
                
                // Apply the inner weight for a smooth transition at the boundary
                return result * innerWeight;
            }
        ENDCG

        Pass
        {
            Name "InverseMask"
            
            Cull Off
            BlendOp Add
            Blend Zero OneMinusSrcColor, Zero OneMinusSrcAlpha
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
                float mask = tex2D(_Mask, i.uv).x;
                if (mask <= 0.005) return float4(0, 0, 0, 0);
                
                mask = ApplyFalloff(mask, _Falloff.x, _Falloff.y, _Falloff.z, _MaskRange.xy, _MaskRange.z);
                
                return float4(mask, mask, mask, mask);
            }
            ENDCG
        }


        Pass
        {
            Name "WriteSplat"
            
            Cull Off
            BlendOp Add
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
                float4 vertex : SV_POSITION;
            };

            sampler2D _Mask;
            float4 _Intensity;
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
                float mask = tex2D(_Mask, i.uv).x;
                if (mask <= 0.005) return float4(0, 0, 0, 0);

                mask = ApplyFalloff(mask, _Falloff.x, _Falloff.y, _Falloff.z, _MaskRange.xy, _MaskRange.z);
                
                // Apply intensity to each channel but preserve interpolation
                float4 result = _Intensity;
                result *= mask;
                
                return result;
            }
            ENDCG
        }
        
        Pass
        {
            Name "WriteSplatSlope"
            
            Cull Off
            BlendOp Add
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
                float2 normalUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _Mask;
            sampler2D _NormalMap;
            float4 _Intensity;
            float4 _Falloff;
            float4 _MaskRange;
            float4 _SlopeRange; // x = min cosine (max angle), y = max cosine (min angle)
            float4 _TerrainUVParams; // x = center X, y = center Y, z = size X, w = size Y in terrain space

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // Get the object-to-world transformation matrix
                float4x4 objectToWorld = unity_ObjectToWorld;
                
                // Create a 2x2 transformation matrix for the XZ plane (to handle both scale and rotation)
                // We need to handle the translation separately since we're working with UV coordinates
                float2x2 transformXZ = float2x2(
                    objectToWorld._11, objectToWorld._13,
                    objectToWorld._31, objectToWorld._33
                );
                
                // Center the UV coordinates (-0.5 to 0.5)
                float2 centeredUV = v.uv - 0.5;
                
                // Apply the transformation (scale and rotation)
                float2 transformedUV = mul(transformXZ, centeredUV);
                
                // Map the transformed UV to terrain space
                float2 normalUV = _TerrainUVParams.xy + transformedUV;
                
                o.normalUV = normalUV;
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Sample mask and normal
                float mask = tex2D(_Mask, i.uv).x;
                if (mask <= 0.005) return float4(0, 0, 0, 0);
                
                // Sample the normal map using terrain-space UV coordinates
                float3 normal = tex2D(_NormalMap, i.normalUV).xyz;
                
                // Convert normal from 0-1 range to -1 to 1 range
                normal = normal * 2.0 - 1.0;
                
                // Calculate slope as the cosine of the angle from up vector (dot product)
                float slope = normal.y; // This is the cosine of the angle
                
                // Check if slope is within range
                // _SlopeRange.x = min cosine (steepest angle allowed)
                // _SlopeRange.y = max cosine (shallowest angle allowed)
                float slopeFactor = 0;
                if (slope >= _SlopeRange.x && slope <= _SlopeRange.y)
                {
                    // Calculate smooth transition at slope boundaries
                    const float SLOPE_SMOOTH = 0.05; // Blend width for smooth transition
                    
                    // Lower boundary (max angle/min cosine)
                    float lowerBlend = smoothstep(_SlopeRange.x - SLOPE_SMOOTH, _SlopeRange.x + SLOPE_SMOOTH, slope);
                    
                    // Upper boundary (min angle/max cosine)
                    float upperBlend = smoothstep(_SlopeRange.y + SLOPE_SMOOTH, _SlopeRange.y - SLOPE_SMOOTH, slope);
                    
                    // Combine the two blend factors
                    slopeFactor = lowerBlend * upperBlend;
                }
                
                // Apply mask falloff
                mask = ApplyFalloff(mask, _Falloff.x, _Falloff.y, _Falloff.z, _MaskRange.xy, _MaskRange.z);
                
                // Combine slope factor with mask
                mask *= slopeFactor;
                
                // Apply intensity to each channel
                float4 result = _Intensity;
                result *= mask;
                
                return result;
            }
            ENDCG
        }
    }
}