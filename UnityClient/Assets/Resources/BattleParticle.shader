Shader "LocalLab/BattleParticle"
{
    Properties { _MainTex("Original particle",2D)="black"{} _Tint("Tint",Color)=(1,1,1,1) _Threshold("Luminance threshold",Float)=0 _SoftMask("Soft particle edge",Float)=0 _KeyBackground("Remove uniform texture background",Float)=0 _Background("Texture background",Color)=(0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _Tint,_Background; float _Threshold,_SoftMask,_KeyBackground;
            fixed4 frag(v2f_img i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv);
                color.rgb=lerp(color.rgb,abs(color.rgb-_Background.rgb),_KeyBackground);
                color.rgb=max(0,color.rgb-_Threshold)/max(.01,1-_Threshold);
                color.a*=lerp(1,pow(saturate(1-length(i.uv-.5)*2),2),_SoftMask);
                return color*_Tint;
            }
            ENDCG
        }
    }
}
