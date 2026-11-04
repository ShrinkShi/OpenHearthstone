Shader "LocalLab/AssetSurface"
{
    Properties { _MainTex("Texture",2D)="white"{} _UVAngle("Portrait UV angle",Float)=0 _Tint("Tint",Color)=(1,1,1,1) _UseAlpha("Alpha mask",Float)=0 _VertexTint("Vertex color",Float)=0 _Effect("Effect luminance",Float)=0 _Multiply("Multiply",Float)=0 _ZWrite("Depth write",Int)=1 _SrcBlend("Source blend",Int)=5 _DstBlend("Destination blend",Int)=10 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Tint; float _UseAlpha, _VertexTint, _Effect, _Multiply, _UVAngle;
            struct appdata {float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR;};
            v2f vert(appdata v) {v2f o; o.pos=UnityObjectToClipPos(v.vertex); float a=radians(_UVAngle); float2 p=v.uv-.5; float2 uv=float2(p.x*cos(a)-p.y*sin(a),p.x*sin(a)+p.y*cos(a))+.5; o.uv=TRANSFORM_TEX(uv,_MainTex); o.color=lerp(1,v.color,_VertexTint); return o;}
            fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*_Tint*i.color; c.a=lerp(_Tint.a,c.a,_UseAlpha); c.a*=lerp(1,max(c.r,max(c.g,c.b))*.15,_Effect); c.rgb=lerp(c.rgb,lerp(1,c.rgb,c.a),_Multiply); clip(c.a-.01); return c;}
            ENDCG
        }
    }
}
