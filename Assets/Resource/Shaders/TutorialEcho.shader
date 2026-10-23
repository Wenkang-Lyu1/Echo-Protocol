Shader "EchoProtocol/TutorialEcho"
{
    Properties { _Color ("Tint", Color) = (.15,.85,1,.28) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct output { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            fixed4 _Color;
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.normal=UnityObjectToWorldNormal(v.normal); return o; }
            fixed4 frag(output i):SV_Target
            {
                float rim=pow(1-abs(dot(normalize(_WorldSpaceCameraPos-i.world),normalize(i.normal))),2);
                float scan=.85+.15*sin(i.world.y*65-_Time.y*5);
                return fixed4(_Color.rgb*(.8+rim)*scan,saturate(_Color.a+rim*.4));
            }
            ENDCG
        }
    }
}
