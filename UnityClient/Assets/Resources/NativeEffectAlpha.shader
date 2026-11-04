Shader "LocalLab/NativeEffectCaptureAlpha"
{
    Properties { _MainTex("Capture",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                // Board capture compatibility: additive RGB must not carry an
                // opaque black alpha from the original internal RT clear.
                c.a=min(c.a,max(c.r,max(c.g,c.b)));return c;
            }
            ENDCG
        }
    }
}
