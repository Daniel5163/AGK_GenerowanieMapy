Shader "Custom/TerrainStable"
{
    Properties
    {
        _Sand ("Sand", 2D) = "white" {}
        _Grass ("Grass", 2D) = "white" {}
        _Rock ("Rock (Gray Mountains)", 2D) = "white" {}
        _Snow ("Snow", 2D) = "white" {}
        _WaterColor ("Water Color", Color) = (0,0.4,0.8,0.6)
        _SeaLevel ("Sea Level (0-1)", Range(0,1)) = 0.26
        _TriplanarScale ("Texture Scale", Float) = 12
        _SnowStart ("Snow Start Height", Range(0,1)) = 0.68
        _SnowFade ("Snow Fade", Range(0,0.3)) = 0.12
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

            sampler2D _Sand, _Grass, _Rock, _Snow;
            float4 _WaterColor;
            float _SeaLevel;
            float _TriplanarScale;
            float _SnowStart;
            float _SnowFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height01 : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.height01 = saturate(v.vertex.y / 120.0);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i), b = hash(i + float2(1,0));
                float c = hash(i + float2(0,1)), d = hash(i + float2(1,1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float h = i.height01;
                float2 uv = i.uv * _TriplanarScale;

                fixed4 sand  = tex2D(_Sand, uv);
                fixed4 grass = tex2D(_Grass, uv);
                fixed4 rock  = tex2D(_Rock, uv);
                fixed4 snow  = tex2D(_Snow, uv);

                float sandMask  = 1 - smoothstep(0.20, 0.32, h);
                float grassMask = smoothstep(0.25, 0.48, h) * (1 - smoothstep(0.52, 0.62, h));
                float rockMask  = smoothstep(0.45, 0.72, h);
                float snowMask  = smoothstep(_SnowStart, _SnowStart + _SnowFade, h);

                fixed4 col = sand * sandMask + 
                             grass * grassMask + 
                             rock * rockMask + 
                             snow * snowMask;

                col = lerp(col, rock * 0.95, rockMask * 0.6);

                float waterMask = 1 - smoothstep(_SeaLevel - 0.03, _SeaLevel + 0.03, h);
                float t = _Time.y * 0.3;
                float wave = noise(i.worldPos.xz * 0.1 + float2(t, t*0.6)) * 0.08;
                float4 waterCol = _WaterColor;
                waterCol.rgb += wave;

                col = lerp(col, waterCol, waterMask * _WaterColor.a);

                return col;
            }
            ENDCG
        }
    }
}