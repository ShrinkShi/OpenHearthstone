Shader "LocalLab/OriginalSilhouetteColor"
{
    Properties {_OriginalGrey("Source constant grey",Float)=1}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _OriginalGrey;
            float4 vert(appdata_base v):SV_POSITION{return UnityObjectToClipPos(v.vertex);}
            float4 frag():SV_Target{return float4(_OriginalGrey,_OriginalGrey,_OriginalGrey,1);}
            ENDCG
        }
    }
}
