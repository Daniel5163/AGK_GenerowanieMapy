Shader "Custom/TerrainShader"
{
    Properties
    {
        _SnowTex("Snow", 2D) = "white" {}
        _RockTex("Rock", 2D) = "white" {}
        _GrassTex("Grass", 2D) = "white" {}
        _SandTex("Sand", 2D) = "white" {}
        _WaterTex("Water", 2D) = "white" {}

        _SnowHeight("Snow", Range(0,1)) = 0.8
        _RockHeight("Rock", Range(0,1)) = 0.6
        _GrassHeight("Grass", Range(0,1)) = 0.4
        _SandHeight("Sand", Range(0,1)) = 0.25
        _WaterHeight("Water", Range(0,1)) = 0.2

        _RockSteepness("Rock Steep", Range(0,1)) = 0.5
        _SnowSteepness("Snow Steep", Range(0,1)) = 0.5

        _TextureScale("Scale", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        #pragma surface surf Standard

        sampler2D _SnowTex;
        sampler2D _RockTex;
        sampler2D _GrassTex;
        sampler2D _SandTex;
        sampler2D _WaterTex;

        float _SnowHeight, _RockHeight, _GrassHeight, _SandHeight, _WaterHeight;
        float _RockSteepness, _SnowSteepness;
        float _TextureScale;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR; 
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float height = IN.color.r; 

            float steep = 1 - abs(dot(IN.worldNormal, float3(0,1,0)));

            float2 uv = IN.worldPos.xz * _TextureScale;

            float4 snow = tex2D(_SnowTex, uv);
            float4 rock = tex2D(_RockTex, uv);
            float4 grass = tex2D(_GrassTex, uv);
            float4 sand = tex2D(_SandTex, uv);
            float4 water = tex2D(_WaterTex, uv);

            float4 col;

            if (height < _WaterHeight)
                col = water;
            else if (height < _SandHeight)
                col = sand;
            else if (height < _GrassHeight)
                col = grass;
            else if (height < _RockHeight)
                col = lerp(grass, rock, steep * _RockSteepness);
            else
                col = lerp(rock, snow, steep * _SnowSteepness);

            o.Albedo = col.rgb;
            o.Metallic = 0;
            o.Smoothness = 0.2;
        }
        ENDCG
    }

    FallBack "Diffuse"
}