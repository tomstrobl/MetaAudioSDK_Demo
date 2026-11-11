// Unlit glowing ember for mobile VR. A hot centre fades to a red rim (fresnel) with a slow, swirling
// flicker. The same shader draws the opaque core and rune rings and, with additive blending and
// _RimOnly = 1, the expanding sound ripples (only their edges glow, so they read as shells).
Shader "MetaAudioSDKDemo/Ember"
{
    Properties
    {
        _HotColor ("Hot Colour", Color) = (1, 0.82, 0.45, 1)
        _RimColor ("Rim Colour", Color) = (1, 0.16, 0.03, 1)
        _RimPower ("Rim Power", Range(0.1, 8)) = 2
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Noise ("Flicker Amount", Range(0, 1)) = 0.5
        _NoiseScale ("Flicker Scale", Float) = 7
        _RimOnly ("Rim Only", Range(0, 1)) = 0
        _NearFade ("Fade Near The Eye", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half4 _HotColor, _RimColor;
            half _RimPower, _Intensity, _Noise, _RimOnly, _NearFade;
            float _NoiseScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 objPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objPos = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 toEye = _WorldSpaceCameraPos - i.worldPos;
                float dist = length(toEye);
                half ndv = saturate(abs(dot(normalize(i.worldNormal), toEye / dist)));
                half rim = pow(1.0h - ndv, _RimPower);

                float t = _Time.y;
                float3 p = i.objPos * _NoiseScale;
                half n = sin(p.x + t * 3.1) * sin(p.y * 1.13 - t * 2.3) * sin(p.z * 0.91 + t * 2.7);

                half3 col = lerp(_HotColor.rgb, _RimColor.rgb, saturate(rim + _Noise * 0.5h * n));
                half mask = lerp(1.0h, rim, _RimOnly);
                half nearFade = lerp(1.0h, saturate((dist - 0.25) / 0.6), _NearFade);
                return half4(col * (_Intensity * mask * nearFade), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
