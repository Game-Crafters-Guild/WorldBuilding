Shader "Hidden/GameCraftersGuild/TerrainGen/GenerateSplinePathMask"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _LocalBoundsMinY ("Local Bounds Min Y", Float) = 0.0
        _LocalBoundsMaxY ("Local Bounds Max Y", Float) = 1.0
        _IsStraightLine ("Is Straight Line", Float) = 0.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 normal : NORMAL;
            };
            
            float4 _Color;
            float _LocalBoundsMinY;
            float _LocalBoundsMaxY;
            float _IsStraightLine;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.localPos = v.vertex.xyz; // Store local position
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // Calculate mask intensity based on UV 
                // Stronger in center (1.0), weaker at edges (0.0)
                float maskIntensity = 1.0 - abs(i.uv.x);
                
                // Apply more gradual falloff for long paths
                if (_IsStraightLine > 0.5) {
                    maskIntensity = pow(maskIntensity, 0.8); // Less falloff for straight lines
                } else {
                    // More consistent falloff that doesn't vary with path length
                    maskIntensity = pow(maskIntensity, 0.95);
                }
                
                // CRITICAL FIX: Don't derive height from world position - use actual Y position directly
                // This ensures we're using the exact height from the spline
                float height = i.localPos.y;
                
                // Calculate normalized height (0-1) based on the object's local bounds
                float heightRange = max(0.001, _LocalBoundsMaxY - _LocalBoundsMinY);
                float normalizedHeight = (height - _LocalBoundsMinY) / heightRange;
                
                // Clamp to ensure valid range
                normalizedHeight = saturate(normalizedHeight);
                
                // Alpha is the mask intensity (controls the strength of the effect)
                float alpha = maskIntensity * _Color.a;
                
                // Return color: RGB for height and mask, A for strength
                // - Red channel (r): Mask intensity (used for blend strength)
                // - Green channel (g): Used for storing the exact (undivided) height value
                // - Blue channel (b): Height information (normalized height value)
                // - Alpha channel (a): Mask strength for blending
                return float4(maskIntensity, height, normalizedHeight, alpha);
            }
            ENDCG
        }
    }
} 