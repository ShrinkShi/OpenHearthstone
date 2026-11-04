Shader "Hidden/OriginalFrameShadowProbe"
{
    SubShader
    {
        Pass
        {
            Cull Off ZTest Always ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Resources/OriginalActorFrame.cginc"
            float4 _World,_Normal,_Direction,_Bias;float4x4 _VP;
            float4 vert(appdata_base v):SV_POSITION{return UnityObjectToClipPos(v.vertex);}
            float4 frag():SV_Target{return OriginalFrameShadow(_World.xyz,_Normal.xyz,_Direction.xyz,_Bias.xy,_VP);}
            ENDCG
        }
    }
}
