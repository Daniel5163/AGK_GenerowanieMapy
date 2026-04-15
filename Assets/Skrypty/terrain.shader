Shader "Custom/TerrainStable"
{
    Properties
    {
        _Sand ("Sand", 2D) = "white" {}
        _Grass ("Grass", 2D) = "white" {}
        _Rock ("Rock", 2D) = "white" {}
        _Snow ("Snow", 2D) = "white" {}

        _WaterColor ("Water Color", Color) = (0,0.4,0.8,0.6)
        _WaterLevel ("Water Level", Float) = 5
        _HeightMultiplier ("Height Multiplier", Float) = 25
        _FlowSpeed ("Water Flow Speed", Float) = 0.3
        _FlowStrength ("Wave Strength", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _Sand;
            sampler2D _Grass;
            sampler2D _Rock;
            sampler2D _Snow;

            float4 _WaterColor;
            float _WaterLevel;
            float _HeightMultiplier;
            float _FlowSpeed;
            float _FlowStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                float worldY : TEXCOORD2;
                float2 worldXZ : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.worldY = v.vertex.y;
                o.worldXZ = v.vertex.xz;

                o.height = saturate(v.vertex.y / _HeightMultiplier);

                return o;
            }

            float noise(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float h = i.height;

                fixed4 col;

                if (h < 0.3) col = tex2D(_Sand, i.uv * 12);
                else if (h < 0.5) col = tex2D(_Grass, i.uv * 8);
                else if (h < 0.7) col = tex2D(_Rock, i.uv * 6);
                else col = tex2D(_Snow, i.uv * 5);

                float waterMask = step(i.worldY, _WaterLevel);

                float t = _Time.y * _FlowSpeed;

                float2 flowUV = i.worldXZ * 0.1 + float2(t, t * 0.7);

                float wave = noise(flowUV) * _FlowStrength;

                float waterEffect = waterMask * _WaterColor.a;

                fixed4 waterCol = _WaterColor;
                waterCol.rgb += wave;

                col = lerp(col, waterCol, waterEffect);

                return col;
            }
            ENDCG
        }
    }
}