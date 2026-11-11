// A cheap, fully procedural sundown sky for mobile VR (Quest): a dark zenith, a blood-red horizon and a
// low sun with a wide glow. At and below the horizon the sky is exactly _HorizonColor, so when the fog
// colour matches it, the edge of the terrain disappears into the sky.
Shader "MetaAudioSDKDemo/Skybox/Demonic Sundown"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.02, 0.005, 0.02, 1)
        _UpperColor ("Upper Sky", Color) = (0.10, 0.015, 0.035, 1)
        _HorizonColor ("Horizon (match the fog colour)", Color) = (0.20, 0.035, 0.03, 1)
        _GlowColor ("Sun Glow", Color) = (0.95, 0.22, 0.05, 1)
        _SunColor ("Sun Disk", Color) = (1.0, 0.42, 0.16, 1)
        _SunDirection ("Sun Direction (towards the sun)", Vector) = (0, 0.08, 1, 0)
        _SunSize ("Sun Radius (degrees)", Range(0.5, 10)) = 3
        _GlowSpread ("Glow Tightness", Range(1, 64)) = 8
        _GlowHeight ("Glow Height", Range(0.02, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _ZenithColor, _UpperColor, _HorizonColor, _GlowColor, _SunColor;
            float4 _SunDirection;
            float _SunSize, _GlowSpread, _GlowHeight;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 s = normalize(_SunDirection.xyz);
                float h = d.y;

                half3 col = lerp(_HorizonColor.rgb, _UpperColor.rgb, saturate(h / 0.25));
                col = lerp(col, _ZenithColor.rgb, saturate((h - 0.25) / 0.75));

                // Everything below fades in just above the horizon line, so below it the sky is pure horizon colour.
                float above = saturate(h * 40.0);
                float cosAngle = dot(d, s);
                float glow = pow(saturate(cosAngle * 0.5 + 0.5), _GlowSpread) * (1.0 - saturate(h / _GlowHeight));
                col += _GlowColor.rgb * glow * above;

                float sunCos = cos(radians(_SunSize));
                float disk = smoothstep(sunCos - 0.0006, sunCos + 0.0003, cosAngle) * above;
                col = lerp(col, _SunColor.rgb, disk);
                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
