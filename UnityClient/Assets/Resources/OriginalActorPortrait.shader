Shader "LocalLab/OriginalActorPortrait"
{
    Properties
    {
        _MainTex("Portrait",2D)="white"{}
        _SecondTex("Second UV texture",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _SecondTint("Second tint",Color)=(.5,.5,.5,.5)
        _DarkColor("Desaturated dark",Color)=(.3,.3,.3,1)
        _AvgLum("Contrast target",Color)=(.5,.5,.5,1)
        _BlendIntensity("Second intensity",Float)=2
        _Desaturate("Desaturate",Float)=0
        _Contrast("Contrast",Float)=0
        _Intensity("Intensity",Float)=1
        _LightingBlend("Lighting blend",Float)=1
        _MainLightColor("Main light input",Color)=(0,0,0,0)
        _OriginalLightDataZ("Source light enabled input",Float)=0
        _GlobalMipBias("Source global mip bias",Float)=0
        _UVAngle("User portrait calibration",Float)=0
        _Tint("Legacy adapter",Color)=(1,1,1,1)
        _UseAlpha("Legacy adapter",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry" "Highlight"="true"}
        Cull Back ZWrite On ZTest LEqual Blend One Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_SecondTex;
            float4 _MainTex_ST,_SecondTex_ST,_Color,_SecondTint,_DarkColor,_AvgLum,_MainLightColor;
            float _BlendIntensity,_Desaturate,_Contrast,_Intensity,_LightingBlend,_OriginalLightDataZ,_UVAngle,_GlobalMipBias;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD1;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD1;float4 color:COLOR;};
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);
                float a=radians(_UVAngle);float2 p=v.uv-.5;
                float2 uv=float2(p.x*cos(a)-p.y*sin(a),p.x*sin(a)+p.y*cos(a))+.5;
                o.uv=TRANSFORM_TEX(uv,_MainTex);o.uv2=TRANSFORM_TEX(v.uv2,_SecondTex);
                // Original DXBC reads only the constant SH terms, then converts to gamma.
                float3 lighting=saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),1.0/2.4)-.055);
                lighting+=_MainLightColor.rgb*_OriginalLightDataZ;
                o.color=lerp(v.color,float4(lighting,1),_LightingBlend)*_Color;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 second=tex2Dbias(_SecondTex,float4(i.uv2,0,_GlobalMipBias))*_SecondTint*_BlendIntensity;
                float4 portrait=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias));
                float4 c=lerp(portrait,portrait*second,second.a);
                float luminance=dot(c.rgb,float3(.3,.59,.11));
                c=lerp(c,lerp(_DarkColor,_Color,luminance),_Desaturate);
                c=lerp(c,_AvgLum,_Contrast);
                return min(c,(c+.15)*i.color)*_Intensity;
            }
            ENDCG
        }
    }
}
