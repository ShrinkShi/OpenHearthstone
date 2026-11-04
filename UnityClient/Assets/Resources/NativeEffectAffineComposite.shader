Shader "LocalLab/NativeEffectAffineComposite"
{
    Properties { _MainTex("Colored transmission or emission",2D)="white"{} _Tint("Presentation fade",Color)=(1,1,1,1) }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
        struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
        sampler2D _MainTex;float4 _Tint;
        v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
        float4 transmit(v2f i):SV_Target{return 1+_Tint.a*(tex2D(_MainTex,i.uv)-1);}
        float4 emit(v2f i):SV_Target{float4 c=tex2D(_MainTex,i.uv);c.rgb*=_Tint.rgb*_Tint.a;c.a*=_Tint.a;return c;}
        ENDCG
        Pass
        {
            Blend Zero SrcColor
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment transmit
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment emit
            ENDCG
        }
    }
}
