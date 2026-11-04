// Original Selection postprocess DXBC uses TEXCOORD1, not the portrait UV.
#include "UnityCG.cginc"
sampler2D _MainTex,_BlendTex;
float4 _MainTex_TexelSize,_BlurOffsets;
float _GlobalMipBias;
struct OriginalFilterInput {float4 vertex:POSITION;float2 uv:TEXCOORD1;};
struct OriginalFilterOutput {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 a:TEXCOORD1;float2 b:TEXCOORD2;float2 c:TEXCOORD3;float2 d:TEXCOORD4;};
OriginalFilterOutput OriginalFilterVertex(OriginalFilterInput v)
{
    OriginalFilterOutput o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
    float2 offset=_MainTex_TexelSize.xy*_BlurOffsets.xy;
    o.a=v.uv+offset;o.b=v.uv-offset;o.c=v.uv+offset*float2(1,-1);o.d=v.uv-offset*float2(1,-1);
    return o;
}
float OriginalFilterSum(sampler2D source,OriginalFilterOutput i)
{
    return tex2Dbias(source,float4(i.a,0,_GlobalMipBias)).r+tex2Dbias(source,float4(i.b,0,_GlobalMipBias)).r+
           tex2Dbias(source,float4(i.c,0,_GlobalMipBias)).r+tex2Dbias(source,float4(i.d,0,_GlobalMipBias)).r;
}
