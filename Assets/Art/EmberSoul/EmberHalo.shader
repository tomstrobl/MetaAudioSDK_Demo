// A soft additive glow that always faces the eye. Draw it on a unit quad; the object's scale sets
// the glow's diameter. Cheap enough for mobile VR (one small quad, no textures).
Shader "MetaAudioSDKDemo/Ember Halo"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.3, 0.06, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _Falloff ("Falloff", Range(0.5, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half4 _Color;
            half _Intensity, _Falloff;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 centre = UnityObjectToViewPos(float3(0, 0, 0));
                float size = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float3 viewPos = centre + float3(v.vertex.xy * size, 0);
                o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1));
                o.uv = v.vertex.xy * 2.0;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half a = pow(saturate(1.0h - length(i.uv)), _Falloff);
                return half4(_Color.rgb * (a * _Intensity), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
