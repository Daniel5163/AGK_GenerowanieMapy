Shader "Custom/Terrain5"
{
    Properties
    {
        _Sand ("Sand", 2D) = "white" {}
        _Grass ("Grass", 2D) = "white" {}
        _Rock ("Rock", 2D) = "white" {}
        _Snow ("Snow", 2D) = "white" {}
        _Water ("Water", 2D) = "white" {}
        _WaterLevel ("Water Level", Range(0,1)) = 0.22
        _RiverDarkness ("River Darkness", Range(0,1)) = 0.70
        _RiverWidthFactor ("River Visual Width", Range(0.3,2)) = 1.0
        _SandStart ("Sand Start Height", Range(0,0.5)) = 0.22
        _SandEnd ("Sand End Height", Range(0.2,0.6)) = 0.32
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
            sampler2D _Water;
            float _WaterLevel;
            float _RiverDarkness;
            float _RiverWidthFactor;
            float _SandStart;
            float _SandEnd;

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
                float worldX : TEXCOORD2;
                float worldZ : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.height = v.vertex.y / 25.0;
                o.worldX = v.vertex.x;
                o.worldZ = v.vertex.z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float h = i.height;

                fixed4 sand  = tex2D(_Sand,  i.uv * 12);
                fixed4 grass = tex2D(_Grass, i.uv * 8);
                fixed4 rock  = tex2D(_Rock,  i.uv * 6.5);
                fixed4 snow  = tex2D(_Snow,  i.uv * 5);
                fixed4 water = tex2D(_Water, i.uv * 5 + _Time.y * float2(0.05, 0.07));

                float river = smoothstep(_WaterLevel + 0.04 * _RiverWidthFactor, _WaterLevel, h);
                river *= smoothstep(_WaterLevel - 0.01, _WaterLevel + 0.22, h);
                
                float sandAmount = 0;
                if (h < _SandEnd && h > _SandStart)
                {
                    sandAmount = 1 - smoothstep(_SandStart, _SandEnd, h);
                    sandAmount = pow(sandAmount, 1.5);
                }
                
                if (h < _WaterLevel + 0.05 && h > _WaterLevel)
                {
                    sandAmount = max(sandAmount, 1 - (h - _WaterLevel) / 0.05);
                }
                
                if (river > 0.05)
                {
                    sandAmount = max(sandAmount, river * 0.8);
                }

                if (h < _WaterLevel)
                {
                    fixed4 shallowWater = water * 1.15;
                    if (h > _WaterLevel - 0.08)
                    {
                        float sandInWater = 1 - (h - (_WaterLevel - 0.08)) / 0.08;
                        sandInWater = pow(sandInWater, 0.7);
                        fixed4 underwaterSand = tex2D(_Sand, i.uv * 10) * 0.9;
                        shallowWater = lerp(shallowWater, underwaterSand, sandInWater * 0.6);
                    }
                    return shallowWater;
                }

                fixed4 col;
                
                if (sandAmount > 0.3)
                {
                    col = sand;
                    col = lerp(col, sand * 1.2, sandAmount);
                }
                else if (h < 0.36) col = sand;
                else if (h < 0.55) col = grass;
                else if (h < 0.74) col = rock;
                else col = snow;

                if (sandAmount > 0 && sandAmount <= 0.3)
                {
                    fixed4 baseCol;
                    if (h < 0.36) baseCol = sand;
                    else if (h < 0.55) baseCol = grass;
                    else if (h < 0.74) baseCol = rock;
                    else baseCol = snow;
                    
                    col = lerp(baseCol, sand, sandAmount * 2);
                }

                if (river > 0.02)
                {
                    fixed4 riverColor = lerp(sand * 0.5, fixed4(0.12, 0.22, 0.38, 1), 0.35);
                    col = lerp(col, riverColor, river * _RiverDarkness);
                    
                    if (river > 0.15)
                    {
                        fixed4 wetSand = sand * 0.7;
                        col = lerp(col, wetSand, river * 0.5);
                    }
                }

                return col;
            }
            ENDCG
        }
    }
}