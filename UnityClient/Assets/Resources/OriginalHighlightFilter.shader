Shader "LocalLab/OriginalHighlightFilter"
{
    Properties {_MainTex("Source",2D)="black"{} _BlendTex("Blend",2D)="black"{}}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One Zero
        Pass
        {
            Name "MultiSample"
            CGPROGRAM
            #pragma vertex OriginalFilterVertex
            #pragma fragment frag
            #include "OriginalHighlightFilter.cginc"
            float4 frag(OriginalFilterOutput i):SV_Target{return OriginalFilterSum(_MainTex,i)*.25;}
            ENDCG
        }
        Pass
        {
            Name "MultiSampleBlend"
            CGPROGRAM
            #pragma vertex OriginalFilterVertex
            #pragma fragment frag
            #include "OriginalHighlightFilter.cginc"
            float4 frag(OriginalFilterOutput i):SV_Target{return max(OriginalFilterSum(_MainTex,i)*.25,tex2Dbias(_BlendTex,float4(i.uv,0,_GlobalMipBias)).r);}
            ENDCG
        }
        Pass
        {
            Name "MaskBlend"
            CGPROGRAM
            #pragma vertex OriginalFilterVertex
            #pragma fragment frag
            #include "OriginalHighlightFilter.cginc"
            float4 frag(OriginalFilterOutput i):SV_Target{return saturate(.3*(OriginalFilterSum(_MainTex,i)-OriginalFilterSum(_BlendTex,i)));}
            ENDCG
        }
    }
}
