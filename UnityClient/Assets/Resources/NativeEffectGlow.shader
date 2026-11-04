Shader "LocalLab/OriginalParticleGlow"
{
    Properties
    {
        _MainTex("Original main",2D)="white"{}
        _Color("Original tint",Color)=(1,1,1,1)
        _GlowColor("Original glow tint",Color)=(1,1,1,1)
        _GlowIntensity("Original glow intensity",Float)=0
        _GlowAddMul("Original glow alpha multiplier",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent+99" "RenderType"="Transparent"}
        Cull Off ZWrite Off ZTest LEqual
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;float4 _MainTex_ST,_Color,_GlowColor,_GlobalMipBias;float _GlowIntensity,_GlowAddMul;
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;
                o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                // Original DXBC GlowTransparent: tint RGB never enters this pass.
                float4 energy=2*i.color*tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                float brightness=dot(energy.rgb,float3(.333,.333,.333));
                float4 glow=energy.a*energy*_Color.a;
                glow.rgb*=_GlowColor.rgb;glow.a*=_GlowAddMul;
                return .25*brightness*glow*_GlowIntensity;
            }
            ENDCG
        }
    }
}
