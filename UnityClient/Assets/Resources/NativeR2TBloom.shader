Shader "LocalLab/OriginalR2TBloom"
{
    Properties{_MainTex("Source",2D)="black"{} _Cutoff("Source exponent",Float)=15 _AlphaIntensity("Alpha multiplier",Float)=1 _ColorIntensity("Color multiplier",Float)=1 _Intensity("Gain",Float)=1 _Threshold("Threshold",Float)=0}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One Zero
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;float4 _Offset;float _Cutoff,_AlphaIntensity,_ColorIntensity,_Intensity,_Threshold;
        float4 threshold(v2f_img i):SV_Target
        {
            float4 value=tex2D(_MainTex,i.uv);
            value=max(value,tex2D(_MainTex,i.uv+_Offset.xy));value=max(value,tex2D(_MainTex,i.uv-_Offset.xy));
            value=max(value,tex2D(_MainTex,i.uv+float2(_Offset.x,-_Offset.y)));value=max(value,tex2D(_MainTex,i.uv+float2(-_Offset.x,_Offset.y)));
            return saturate(float4(value.rgb*_ColorIntensity,pow(value.a*_AlphaIntensity,_Cutoff))-_Threshold)*_Intensity;
        }
        float4 blur(v2f_img i):SV_Target
        {
            float4 value=tex2D(_MainTex,i.uv+_Offset.xy*1.5)+tex2D(_MainTex,i.uv-_Offset.xy*1.5)+tex2D(_MainTex,i.uv+_Offset.xy*.5)+tex2D(_MainTex,i.uv-_Offset.xy*.5);
            return value*float4(_ColorIntensity,_ColorIntensity,_ColorIntensity,1)*.25;
        }
        ENDCG
        Pass
        {
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment threshold
        ENDCG
        }
        Pass
        {
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment blur
        ENDCG
        }
    }
}
