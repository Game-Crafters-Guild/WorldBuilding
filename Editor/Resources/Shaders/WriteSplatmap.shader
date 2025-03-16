Shader "Hidden/GameCraftersGuild/TerrainGen/WriteSplatmap"
{
    Properties
    {
        [NoScaleOffset]_Mask("Mask", 2D) = "white" {}
        [NoScaleOffset]_Splatmap("Splatmap", 2D) = "black" {}
        _Intensity("Intensity", Color) = (0, 0, 0, 0)
        _Falloff("Falloff", Vector) = (0, 1, 0, 0)
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
                
                float falloffRange = max(0.01, _Falloff.y - _Falloff.x);
                mask = saturate(smoothstep(_Falloff.x, _Falloff.y, mask));
                
                mask = pow(mask, 0.9);
                
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
                
                float falloffRange = max(0.01, _Falloff.y - _Falloff.x);
                mask = saturate(smoothstep(_Falloff.x, _Falloff.y, mask));
                
                mask = pow(mask, 0.9);
                
                return _Intensity * mask;
            }
            ENDCG
        }
    }
}
