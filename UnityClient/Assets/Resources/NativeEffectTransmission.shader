Shader "LocalLab/NativeEffectTransmission"
{
    Properties { _MainTex("One replay",2D)="white"{} _BlackTex("Zero replay",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_BlackTex;
            float4 frag(v2f_img i):SV_Target{return tex2D(_MainTex,i.uv)-tex2D(_BlackTex,i.uv);}
            ENDCG
        }
    }
}
