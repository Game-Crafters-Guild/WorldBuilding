Shader "Hidden/GameCraftersGuild/TerrainGen/WriteSplatmap"
{
    Properties
    {
        [NoScaleOffset]_Mask("Mask", 2D) = "white" {}
        [NoScaleOffset]_Splatmap("Splatmap", 2D) = "black" {}
        _Intensity("Intensity", Color) = (0, 0, 0, 0)
        _Falloff("Falloff", Vector) = (0, 1, 0, 0)
        _MaskRange("MaskRange", Vector) = (0, 1, 0, 0)
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
    }
}
