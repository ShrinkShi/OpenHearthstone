Shader "LocalLab/InteractionGlow"
{
    Properties { _MainTex("Source",2D)="white"{} _Radius("Radius",Float)=16 _Padding("Padding",Float)=32 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; float _Radius,_Padding;
            float mask(float2 uv)
            {
                if(any(uv<0)||any(uv>1))return 0;
                return smoothstep(.08,.75,tex2D(_MainTex,uv).a);
            }
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv*(1+2*_Padding*_MainTex_TexelSize.xy)-_Padding*_MainTex_TexelSize.xy;
                float glow=0,weight=0;
                [unroll]for(int step=1;step<=6;step++)
                {
                    float2 d=_MainTex_TexelSize.xy*_Radius*(step/6.0);
                    float a=mask(uv+float2(d.x,0))+mask(uv-float2(d.x,0))+mask(uv+float2(0,d.y))+mask(uv-float2(0,d.y));
                    a+=mask(uv+d*.7071)+mask(uv-d*.7071)+mask(uv+float2(d.x,-d.y)*.7071)+mask(uv+float2(-d.x,d.y)*.7071);
                    float w=exp(-3.0*step*step/36.0);glow+=a*w;weight+=8*w;
                }
                return float4(1,1,1,saturate(glow/weight*3.6)*(1-mask(uv)));
            }
            ENDCG
        }
    }
}
