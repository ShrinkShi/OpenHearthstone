Shader "LocalLab/HeroFragment"
{
    Properties { _MainTex ("Portrait", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _UVRect,_Tint;
            float _Break;
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,_UVRect.xy+i.uv*_UVRect.zw);
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                c.a*=lerp(1,smoothstep(0,.025,edge),_Break)*_Tint.a;
                c.rgb=lerp(c.rgb,dot(c.rgb,float3(.299,.587,.114)).xxx,_Break*.65)*_Tint.rgb;
                return c;
            }
            ENDCG
        }
    }
}
