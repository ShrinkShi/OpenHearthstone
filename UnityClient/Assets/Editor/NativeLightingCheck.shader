Shader "LocalLab/Tests/OriginalParticleLighting"
{
    Properties{_SH("SH L0",Vector)=(0,0,0,0) _MainLight("Main light",Vector)=(0,0,0,0) _Blend("Blend",Float)=0}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Resources/NativeEffectMath.cginc"
            float4 _SH,_MainLight;float _Blend;
            float4 frag(v2f_img i):SV_Target{return float4(OriginalParticleLighting(_SH.rgb,_MainLight.rgb,_Blend),1);}
            ENDCG
        }
    }
}
