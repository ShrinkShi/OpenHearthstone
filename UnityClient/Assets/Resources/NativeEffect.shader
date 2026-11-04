Shader "LocalLab/NativeEffectCompatibility"
{
    Properties
    {
        _MainTex("Original main",2D)="white"{} _FxTex("Original effect",2D)="white"{} _MaskTex("Original mask",2D)="white"{}
        _RampTex("Original area render ramp",2D)="white"{} _RampIntensity("Original area render ramp intensity",Float)=1
        _Color("Original tint",Color)=(1,1,1,1) _FxColor("Effect tint",Color)=(1,1,1,1)
        _SecondColor("Original sneaky smoke inner color",Color)=(1,1,1,1)
        _SecondTint("Original portrait secondary tint",Color)=(1,1,1,1) _BlendIntensity("Original portrait blend intensity",Float)=1
        _ScrollingTex("Original mana scrolling texture",2D)="white"{} _tint("Original mana glow tint",Color)=(1,1,1,1) _Fade("Original mana fade",Color)=(1,1,1,1)
        _XSpeed("Original mana scroll X",Float)=0 _YSpeed("Original mana scroll Y",Float)=0
        _HiColor("Original highlight hot color",Color)=(.5,.5,.5,1) _Colorize("Original highlight colorize",Color)=(1,1,1,1)
        _ColorIntensity("Original highlight color intensity",Float)=1 _FlareTex("Original highlight flare",2D)="white"{}
        _FlareIntensity("Original highlight flare intensity",Float)=1 _VflareSpeed("Original highlight V speed",Float)=1
        _UflareScale("Original highlight U scale",Float)=1 _VflareScale("Original highlight V scale",Float)=1
        _ColorAlphaWeight("Original highlight alpha weight",Float)=1 _TexCoordTwoWeight("Original highlight UV weight",Float)=1 _Seed("Original highlight seed",Float)=0
        _OutlineColor("Original text outline",Color)=(0,0,0,1) _OutlineOffsetX("Original outline X offset",Float)=0 _OutlineOffsetY("Original outline Y offset",Float)=0
        _OffsetX("Original title AA X",Float)=0 _OffsetY("Original title AA Y",Float)=0 _Edge("Original title AA edge",Float)=.585
        _GlowColor("Original glow tint",Color)=(1,1,1,1) _GlowIntensity("Original glow intensity",Float)=0 _GlowAddMul("Original glow alpha multiplier",Float)=0
        _LightingBlend("Original ambient blend",Float)=0
        _DiffuseVertexLighting("Explicit no-shadow source lighting stage",Float)=0
        _DiffuseMainPosition("Explicit main light direction",Vector)=(0,0,0,0)
        _DiffuseMainColor("Explicit main light color",Vector)=(0,0,0,0)
        _DiffuseMainVisibility("Explicit main light visibility",Float)=0
        _DiffuseAdditionalCount("Explicit indexed additional light count",Float)=0
        _TintColor("Particle tint",Color)=(1,1,1,1) _ParticleTint("Particle family",Float)=0 _LuminanceAlpha("Transparent capture approximation",Float)=0
        _Intensity("Intensity",Float)=1 _FxIntensity("Effect intensity",Float)=1 _AlphaIntensity("Alpha intensity",Float)=1 _ColorMul("Color multiplier",Float)=1
        _AlphaMul("Original CommonFX alpha multiplier",Float)=1 _ColorAdd("Original CommonFX RGB additive",Float)=0
        _ColorMul2("Original CommonFX dissolve edge multiplier",Float)=1 _DissolveFactor("Original CommonFX dissolve factor",Float)=0
        _DissolveSmoothness("Original CommonFX dissolve smoothness",Float)=.1 _MinAlpha("Original CommonFX minimum vertex alpha",Float)=0
        _IgnoreVertexAlpha("Original CommonFX ignored vertex alpha",Float)=0
        _Alpha("Original dissolve RGB multiplier",Float)=1 _DissolveIntensity("Original dissolve edge gain",Float)=20
        _Clip("Original holy render serialized clip",Float)=1
        _RgbSrcBlend("Original property-backed RGB source",Float)=5 _RgbDstBlend("Original property-backed RGB destination",Float)=10
        _AlphaSrcBlend("Original property-backed alpha source",Float)=5 _AlphaDstBlend("Original property-backed alpha destination",Float)=10
        _SrcBlendOutline("Original CommonFX source blend",Float)=1 _DstBlendOutline("Original CommonFX destination blend",Float)=10
        _PremultipliedRGBTexture("Original CommonFX RGB alpha floor",Float)=1 _ClampUV("Original main clamp",Float)=0 _SwapUV("Original main swap",Float)=0
        _SecondTex("Original CommonFX second texture",2D)="white"{} _Color2("Original second mask tint",Color)=(1,1,1,1)
        _LookupTex("Original saved lookup (unused by Ramp branch)",2D)="white"{}
        _SecondTexSpeed_NoiseForce("Original second speed and noise force",Vector)=(0,0,0,0)
        _SecondTexClampUV("Original second clamp",Float)=0 _SecondTexSwapUV("Original second swap",Float)=0
        _WhiteColor("Original ramp white",Color)=(1,1,1,1) _MidColor("Original ramp middle",Color)=(1,1,1,1) _LastColor("Original ramp black",Color)=(0,0,0,1)
        _MiddlePointPos("Original ramp threshold",Float)=.5
        _ScaleX("X scale",Float)=1 _ScaleY("Y scale",Float)=1 _ScrollX("X scroll",Float)=0 _ScrollY("Y scroll",Float)=0
        _FxScaleX("Original distortion X scale",Float)=1 _FxScaleY("Original distortion Y scale",Float)=1 _FxScrollX("Original distortion X scroll",Float)=0 _FxScrollY("Original distortion Y scroll",Float)=0
        _RotSpeed("Original rotation radians",Float)=5 _RotPivotX("Original rotation pivot X",Float)=0 _RotPivotY("Original rotation pivot Y",Float)=0
        _FxTex1("Original FX layer one",2D)="black"{} _FxTex2("Original FX layer two",2D)="black"{}
        _FxIntensity1("Original FX gain one",Float)=1 _FxIntensity2("Original FX gain two",Float)=1
        _Scroll1X("Original layer one X scroll",Float)=0 _Scroll1Y("Original layer one Y scroll",Float)=0 _Scale1X("Original layer one X scale",Float)=1 _Scale1Y("Original layer one Y scale",Float)=1
        _Scroll2X("Original layer two X scroll",Float)=0 _Scroll2Y("Original layer two Y scroll",Float)=0 _Scale2X("Original layer two X scale",Float)=1 _Scale2Y("Original layer two Y scale",Float)=1
        _AlphaNoise("Original particle alpha noise",2D)="white"{}
        _USpeed("Layer U speed",Float)=0 _VSpeed("Layer V speed",Float)=0 _Scale("Layer scale",Float)=1
        _Clock("Presentation seconds",Float)=0 _HasFx("Effect slot present",Float)=0 _TwoLayer("Two layer family",Float)=0
        _FxWorld("World-space effect UV",Float)=0 _Origin("Isolation origin",Vector)=(0,0,0,0)
        _HasSourceTexture("Original texture dependency present",Float)=0
        _AddMultiply("Original AddMultiply pixel program",Float)=0
        _OriginalFamily("Verified original program family",Float)=0
        _SolidColor("Original UnlitColor pixel program",Float)=0 _ZWrite("Depth write",Float)=0
        _Cull("Original culling",Float)=0
        _Cutoff("Original cutout threshold",Float)=.5 _offsetU("Original cutout U offset",Float)=0 _offsetV("Original cutout V offset",Float)=0
        _OriginalSrcBlend("Source data blend",Float)=5 _OriginalDstBlend("Destination data blend",Float)=1
        _OriginalSrcAlpha("Source data alpha blend",Float)=1 _OriginalDstAlpha("Destination data alpha blend",Float)=10
        _CaptureSrcAlpha("Capture source opacity",Float)=1 _CaptureDstAlpha("Capture destination opacity",Float)=10
        _SrcBlend("Source blend",Float)=5 _DstBlend("Destination blend",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull [_Cull] ZWrite [_ZWrite] ZTest LEqual
        Blend [_SrcBlend] [_DstBlend], [_CaptureSrcAlpha] [_CaptureDstAlpha]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "NativeEffectMath.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 uv1:TEXCOORD1;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float2 world:TEXCOORD1;float2 layerTwo:TEXCOORD2;float3 normal:TEXCOORD3;float3 positionWS:TEXCOORD4;float3 diffuseVertex:TEXCOORD5;float dissolve:TEXCOORD6;float4 commonFxAlpha:COLOR1;};
            sampler2D _MainTex,_FxTex,_MaskTex,_SecondTex,_AlphaNoise;float4 _MainTex_ST,_FxTex_ST,_SecondTex_ST,_Color,_FxColor,_TintColor,_Origin,_GlobalMipBias,_SecondColor;
            float _AlphaMul,_ColorAdd,_ColorMul2,_PremultipliedRGBTexture,_ClampUV,_SwapUV,_SecondTexClampUV,_SecondTexSwapUV,_MiddlePointPos;
            float _DissolveFactor,_DissolveSmoothness,_MinAlpha,_IgnoreVertexAlpha;
            float _Alpha,_DissolveIntensity;
            float4 _Color2,_SecondTexSpeed_NoiseForce,_WhiteColor,_MidColor,_LastColor;
            float _ParticleTint,_LuminanceAlpha,_FxWorld,_AddMultiply,_SolidColor,_OriginalFamily;
            float _LightingBlend;float4 _MainLightColor;
            float _DiffuseVertexLighting,_DiffuseMainVisibility,_DiffuseAdditionalCount;float4 _DiffuseMainPosition,_DiffuseMainColor;
            float4 _DiffuseLightPosition[8],_DiffuseLightColor[8],_DiffuseLightAttenuation[8],_DiffuseLightSpotDirection[8];
            float3 DiffuseGammaSH()
            {return saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),.4166667)-.055);}
            float3 DiffuseAdditional(float3 normal,float3 positionWS)
            {
                float3 sum=0;
                for(int light=0;light<(int)_DiffuseAdditionalCount;light++)
                {
                    float3 delta=_DiffuseLightPosition[light].xyz-positionWS*_DiffuseLightPosition[light].w;
                    float d2=max(dot(delta,delta),.00006103515625);float3 direction=delta*rsqrt(d2);
                    float attenuation=1/(1+25*d2*_DiffuseLightAttenuation[light].x);
                    float spot=saturate(dot(_DiffuseLightSpotDirection[light].xyz,direction)*_DiffuseLightAttenuation[light].z+_DiffuseLightAttenuation[light].w);
                    sum+=_DiffuseLightColor[light].rgb*(attenuation*spot*spot*saturate(dot(normal,direction)));
                }
                return sum;
            }
            float _Cutoff,_offsetU,_offsetV,_BlendIntensity,_XSpeed,_YSpeed;float4 _SecondTint,_tint,_Fade;sampler2D _ScrollingTex;
            float4 _HiColor,_Colorize;float _ColorIntensity,_FlareIntensity,_VflareSpeed,_UflareScale,_VflareScale,_ColorAlphaWeight,_TexCoordTwoWeight,_Seed,_ShaderTime;sampler2D _FlareTex;
            float4 _OutlineColor,_MainTex_TexelSize;float _OutlineOffsetX,_OutlineOffsetY;
            float _OffsetX,_OffsetY,_Edge;
            sampler2D _RampTex;float _RampIntensity;
            float _Intensity,_FxIntensity,_AlphaIntensity,_ColorMul,_ScaleX,_ScaleY,_ScrollX,_ScrollY,_Clock,_HasFx,_TwoLayer,_USpeed,_VSpeed,_Scale;
            float _FxScaleX,_FxScaleY,_FxScrollX,_FxScrollY;float4 _FxTex_TexelSize;
            sampler2D _FxTex1,_FxTex2;float _RotSpeed,_RotPivotX,_RotPivotY,_FxIntensity1,_FxIntensity2;
            float _Scroll1X,_Scroll1Y,_Scale1X,_Scale1Y,_Scroll2X,_Scroll2Y,_Scale2X,_Scale2Y;
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;o.positionWS=mul(unity_ObjectToWorld,v.vertex).xyz;o.world=(o.positionWS-_Origin.xyz).xz;o.layerTwo=0;o.normal=UnityObjectToWorldNormal(v.normal);o.diffuseVertex=0;o.dissolve=0;o.commonFxAlpha=0;
                if(_OriginalFamily==41||_OriginalFamily==42)
                {o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.color=v.color*_Color;}
                else if(_OriginalFamily>=35&&_OriginalFamily<=40)
                {o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.color=(_OriginalFamily==37?float4(1,1,1,1):v.color)*_Color;}
                else if(_OriginalFamily==33)
                {o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.layerTwo=lerp(o.uv,v.uv1*_MainTex_ST.xy+_MainTex_ST.zw,_TexCoordTwoWeight);}
                else if(_OriginalFamily==34)
                {
                    float3 light=saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),1/2.4)-.055)+_MainLightColor.rgb;
                    o.color=v.color*_Color*float4(lerp(float3(1,1,1),light,_LightingBlend),1);o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                }
                else if(_OriginalFamily==31||_OriginalFamily==32)
                {
                    o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    if(_DiffuseVertexLighting>.5)o.diffuseVertex=DiffuseGammaSH()+DiffuseAdditional(o.normal,o.positionWS);
                }
                else if(_OriginalFamily==30)
                {
                    float3 light=saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),1/2.4)-.055)+_MainLightColor.rgb;
                    o.color=_TintColor*float4(lerp(float3(1,1,1),light,_LightingBlend),1);
                    o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.layerTwo=v.uv*float2(_ScaleX,_ScaleY)+_Clock*.05*float2(_XSpeed,_YSpeed);
                }
                else if(_OriginalFamily==28||_OriginalFamily==29)
                {
                    float3 light=saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),1/2.4)-.055)+_MainLightColor.rgb;
                    o.color=_Color*lerp(v.color,float4(light,1),_LightingBlend);
                    o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;o.layerTwo=v.uv1*_SecondTex_ST.xy+_SecondTex_ST.zw;
                }
                else if(_OriginalFamily==25||_OriginalFamily==26||_OriginalFamily==27)
                {
                    float3 light=saturate(1.055*pow(abs(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w)),1/2.4)-.055)+_MainLightColor.rgb;
                    o.uv=(v.uv+(_OriginalFamily==27?float2(_offsetU,_offsetV):0))*_MainTex_ST.xy+_MainTex_ST.zw;
                    o.color=_OriginalFamily==26?lerp(v.color,float4(light,1),_LightingBlend):v.color*float4(lerp(float3(1,1,1),light,_LightingBlend),1);
                    if(_OriginalFamily==25)o.color*=_Color;
                }
                else if(_OriginalFamily==24){o.color.a*=_Color.a;o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;}
                else if(_OriginalFamily==21)o.color=1+v.color.a*(v.color-1);
                else if(_OriginalFamily==22)o.color=2*v.color*_Color;
                else if(_OriginalFamily==5||_OriginalFamily==6||_OriginalFamily==15||_OriginalFamily==19||_OriginalFamily==44||_OriginalFamily==45||_OriginalFamily==46)
                {
                    float4 tint=v.color*_Color;float high=saturate(2*max(tint.r,max(tint.g,tint.b))-1);
                    if(_OriginalFamily==44||_OriginalFamily==45)
                    {
                        o.color=float4(2*tint.rgb,1-high)*_AlphaMul;
                        o.commonFxAlpha=float4(0,0,0,max(v.color.a,_PremultipliedRGBTexture));
                        o.dissolve=max(1-v.color.a-_IgnoreVertexAlpha,_MinAlpha*1.01)*(2*_DissolveSmoothness+1)-_DissolveSmoothness;
                    }
                    else
                    {
                        o.color=float4(2*tint.rgb,1-high)*(tint.a*_AlphaMul);
                        if(_OriginalFamily==46)
                        {
                            float4 addTint=v.color*_Color2;float addHigh=saturate(2*max(addTint.r,max(addTint.g,addTint.b))-1);
                            o.commonFxAlpha=float4(2*addTint.rgb*_ColorMul2,1-addHigh)*addTint.a;
                        }
                    }
                    float2 mainUV=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    o.uv=lerp(mainUV,mainUV.yx,_SwapUV);
                    float2 secondUV=v.uv*_SecondTex_ST.xy+_SecondTex_ST.zw;
                    o.world=lerp(secondUV,secondUV.yx,_SecondTexSwapUV)+frac((_Clock*2)*_SecondTexSpeed_NoiseForce.xy);
                }
                else if(_OriginalFamily==10)
                {
                    o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    o.color=float4(v.color.rgb*_Color.rgb*_Alpha*_Intensity,v.color.a);
                }
                else if(_OriginalFamily>0&&_OriginalFamily!=11)
                {
                    if(_OriginalFamily==23)o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    o.color.rgb*=OriginalParticleLighting(float3(unity_SHAr.w,unity_SHAg.w,unity_SHAb.w),(_OriginalFamily==3||_OriginalFamily==4||_OriginalFamily==8||_OriginalFamily==9||_OriginalFamily>=12&&_OriginalFamily!=17&&_OriginalFamily!=18)?_MainLightColor.rgb:float3(0,0,0),_LightingBlend);
                    if(_OriginalFamily==7)o.color*=_Color*_Intensity;
                    if(_OriginalFamily==17)
                    {
                        o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                        o.color*=2*_TintColor;
                    }
                    if(_OriginalFamily==18)
                    {
                        o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                        o.color*=_TintColor*_Intensity;
                    }
                    if(_OriginalFamily==16)
                    {
                        o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                        o.color*=2*_TintColor;
                    }
                    if(_OriginalFamily==8||_OriginalFamily==9)
                    {
                        // FXWorld uses pre-divide clip XY, not world XZ. Original _Time.x is seconds/20.
                        o.world=(_OriginalFamily==8?o.vertex.xy:v.uv)*float2(_ScaleX,_ScaleY)+(_Clock*.05)*float2(_ScrollX,_ScrollY);
                        if(_OriginalFamily==9)o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                        o.color*=2*_Intensity*_FxIntensity*(_OriginalFamily==9?_Color.a:1);
                    }
                    if(_OriginalFamily==12)
                    {
                        o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                        o.world=o.uv*float2(_ScaleX,_ScaleY)+float2(_ScrollX,_ScrollY);
                        o.color*=2*_Intensity*_FxIntensity;
                    }
                    if(_OriginalFamily==13)
                    {
                        float2 centered=(v.uv-.5-float2(_RotPivotX,_RotPivotY))*float2(_ScaleX,_ScaleY);
                        float sine,cosine;sincos(_Clock*.05*_RotSpeed,sine,cosine);
                        o.world=float2(cosine*centered.x+sine*centered.y,-sine*centered.x+cosine*centered.y)+.5;
                        o.color*=2*_Intensity*_FxIntensity*_Color.a;
                    }
                    if(_OriginalFamily==14)
                    {
                        o.world=v.uv*float2(_Scale1X,_Scale1Y)+_Clock*.05*float2(_Scroll1X,_Scroll1Y);
                        o.layerTwo=v.uv*float2(_Scale2X,_Scale2Y)+_Clock*.05*float2(_Scroll2X,_Scroll2Y);
                        o.color*=2*_Intensity*_FxIntensity1*_FxIntensity2;
                    }
                }
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                if(_OriginalFamily==40)
                {
                    float2 offset=float2(_OffsetX,_OffsetY)*_MainTex_ST.xy;
                    float4 center=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    float alpha=tex2Dbias(_MainTex,float4(i.uv+offset,0,_GlobalMipBias.x)).a+tex2Dbias(_MainTex,float4(i.uv-offset,0,_GlobalMipBias.x)).a;
                    alpha+=tex2Dbias(_MainTex,float4(i.uv+float2(offset.x,-offset.y),0,_GlobalMipBias.x)).a+tex2Dbias(_MainTex,float4(i.uv+float2(-offset.x,offset.y),0,_GlobalMipBias.x)).a;
                    return float4(center.rgb,alpha*_Edge)*i.color;
                }
                if(_OriginalFamily>=35&&_OriginalFamily<=39)
                {
                    float center=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).a;
                    if(_OriginalFamily==35)return float4(i.color.rgb,center*i.color.a);
                    float2 offset=float2(_OutlineOffsetX,_OutlineOffsetY)*_MainTex_ST.xy;
                    float total=center;
                    total+=tex2Dbias(_MainTex,float4(i.uv+offset*.6,0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv-offset*.6,0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv+float2(offset.x,-offset.y)*.6,0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv+float2(-offset.x,offset.y)*.6,0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv+float2(0,offset.y),0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv-float2(0,offset.y),0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv+float2(offset.x,0),0,_GlobalMipBias.x)).a;
                    total+=tex2Dbias(_MainTex,float4(i.uv-float2(offset.x,0),0,_GlobalMipBias.x)).a;
                    if(_OriginalFamily==38)return float4(i.color.rgb,(total-center)*i.color.a*.23);
                    return float4(lerp(_OutlineColor.rgb,i.color.rgb,center),saturate(total)*i.color.a);
                }
                if(_OriginalFamily==41)
                {
                    float4 sample=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    return float4(sample.rgb*_Intensity*i.color.rgb,pow(sample.a*_AlphaIntensity,_Cutoff)*i.color.a);
                }
                if(_OriginalFamily==42)
                {
                    float4 sample=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    return sample*sample.a*i.color*_Intensity;
                }
                if(_OriginalFamily==33)
                {
                    float mask=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).r*_Intensity*1.2;
                    float phase=_Seed+_ShaderTime;
                    float flare=tex2Dbias(_FlareTex,float4(i.layerTwo.x*_UflareScale+phase*.05,mask*_VflareScale+phase*_VflareSpeed,0,_GlobalMipBias.x)).r;
                    float strength=2*(flare*i.color.r+mask)*mask*_FlareIntensity;
                    return float4((_Color.rgb+strength*(strength*_HiColor.rgb-_Color.rgb))*_ColorIntensity,(1+_ColorAlphaWeight*(i.color.r-1))*strength)*_Colorize;
                }
                if(_OriginalFamily==43)
                {
                    // 原 AreaEffect_Render base ps_4_0：红通道查色带，颜色 alpha 只乘 RGB。
                    float value=tex2Dbias(_MainTex,float4(i.uv*_MainTex_ST.xy+_MainTex_ST.zw,0,_GlobalMipBias.x)).r*_RampIntensity;
                    float4 ramp=tex2Dbias(_RampTex,float4(value,.5,0,_GlobalMipBias.x));
                    return ramp*float4(_Color.rgb*_Color.a,1)*_Intensity*_Alpha;
                }
                if(_OriginalFamily==34)return float4(tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).rgb+i.color.rgb,0);
                if(_OriginalFamily==31||_OriginalFamily==32)
                {
                    float4 main=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                      float3 normal=normalize(i.normal);
                      float3 light=_DiffuseVertexLighting>.5?i.diffuseVertex:DiffuseGammaSH()+DiffuseAdditional(normal,i.positionWS);
                      light+=_DiffuseMainColor.rgb*_DiffuseMainVisibility*saturate(dot(normal,_DiffuseMainPosition.xyz));
                    return float4(main.rgb*lerp(float3(1,1,1),light,_OriginalFamily==32?1:_LightingBlend),main.a);
                }
                if(_OriginalFamily==30)
                {
                    float4 main=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    float scroll=tex2Dbias(_ScrollingTex,float4(i.layerTwo,0,_GlobalMipBias.x)).r;
                    return float4((main.rgb+1.5*scroll*_tint.rgb*main.a*_Intensity)*_Fade.rgb*i.color.rgb,i.color.a);
                }
                if(_OriginalFamily==28)
                {
                    float4 main=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    float4 second=tex2Dbias(_SecondTex,float4(i.layerTwo,0,_GlobalMipBias.x))*_SecondTint*_BlendIntensity;
                    float3 result=main.rgb+second.a*(main.rgb*second.rgb-main.rgb);
                    return float4(min(result,(result+.15)*i.color.rgb),min(1,1.15*i.color.a));
                }
                if(_OriginalFamily==29)return tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x))*i.color;
                if(_OriginalFamily==25||_OriginalFamily==26||_OriginalFamily==27)
                {
                    float4 source=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    if(_OriginalFamily==27)return float4(source.rgb*_Color.rgb,ceil(source.a*_Color.a-_Cutoff))*i.color;
                    return source*i.color;
                }
                if(_OriginalFamily==24)
                {
                    float mask=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).r*i.color.a;
                    return float4(lerp(_Color.rgb,_SecondColor.rgb,mask),mask)*_Intensity;
                }
                if(_OriginalFamily==23)
                {
                    // The original noise motion uses interpolated vertex R/A,
                    // not time; RGB is the constant material tint.
                    float2 noiseUV=(i.uv+i.color.r-i.color.a*float2(_USpeed,_VSpeed))*_Scale;
                    float noise=tex2Dbias(_AlphaNoise,float4(noiseUV,0,_GlobalMipBias.x)).r;
                    float alpha=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).a;
                    return float4(_Color.rgb,noise*alpha*_AlphaIntensity*i.color.a);
                }
                if(_OriginalFamily==21||_OriginalFamily==22)return i.color;
                // Original FX_HolyRender decodes capture R/G, not RGBA tint.
                // Its vertex program ignores mesh color and its pixel output is unclamped.
                if(_OriginalFamily==20)
                {
                    float4 captured=tex2Dbias(_MainTex,float4(i.uv*_MainTex_ST.xy+_MainTex_ST.zw,0,_GlobalMipBias.x));
                    float rays=2*captured.r;
                    float3 rgb=rays*_Color.rgb*_Color.a+captured.g*_Color2.rgb*_Color2.a;
                    return float4(rgb,rays*_Alpha)*_Intensity*_Alpha;
                }
                if(_SolidColor>.5)return _Color;
                if(_OriginalFamily==11)return float4(0,0,0,1);
                if(_OriginalFamily==16)return tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x))*i.color;
                if(_OriginalFamily==17)return tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x))*i.color*float4(_Intensity,_Intensity,_Intensity,_Alpha);
                if(_OriginalFamily==18)
                {
                    float3 rgb=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).rgb;
                    return float4(2*i.color.rgb,saturate(dot(rgb,float3(.3,.59,.11))*_AlphaIntensity)*i.color.a);
                }
                if(_OriginalFamily==10)
                {
                    float red=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x)).r;
                    // Original PS has an upper-only min; clamp follows multiplication.
                    float edge=min((red+i.color.a-1)*_DissolveIntensity,1);
                    return saturate(red*edge*i.color);
                }
                if(_OriginalFamily==46)
                {
                    float2 secondUV=lerp(i.world,saturate(i.world),_SecondTexClampUV);
                    float2 mainUV=lerp(i.uv,saturate(i.uv),_ClampUV);
                    float4 second=tex2Dbias(_SecondTex,float4(secondUV,0,_GlobalMipBias.x));
                    float4 original=tex2Dbias(_MainTex,float4(mainUV,0,_GlobalMipBias.x));
                    float alphaFloor=max(original.a,_PremultipliedRGBTexture);
                    float3 body=(original.rgb+_ColorAdd)*alphaFloor*_ColorMul;
                    float4 added=float4(second.rgb,second.r)*i.commonFxAlpha;
                    return float4((body+added.rgb)*i.color.rgb,(original.a+added.a)*i.color.a);
                }
                if(_OriginalFamily==44||_OriginalFamily==45)
                {
                    float2 secondUV=lerp(i.world,saturate(i.world),_SecondTexClampUV);
                    float4 second=tex2Dbias(_SecondTex,float4(secondUV,0,_GlobalMipBias.x));
                    float maskChannel=_OriginalFamily==44?second.r:second.a;
                    float dissolve=pow(saturate(maskChannel-(2*(i.dissolve+_DissolveFactor)-1)),10*_DissolveSmoothness);
                    float2 mainUV=lerp(i.uv,saturate(i.uv),_ClampUV);
                    float4 original=tex2Dbias(_MainTex,float4(mainUV,0,_GlobalMipBias.x));
                    float alphaFloor=max(original.a,_PremultipliedRGBTexture);
                    float3 body=(original.rgb+_ColorAdd)*alphaFloor*_ColorMul*dissolve;
                    float edge=original.a*saturate(dissolve*original.a*1000000)*pow(1-dissolve,10);
                    float3 edgeRgb=edge*_Color2.rgb*_ColorMul2*i.commonFxAlpha.a;
                    return float4((body+edgeRgb)*i.color.rgb,dissolve*original.a*i.color.a);
                }
                if(_OriginalFamily==5||_OriginalFamily==6||_OriginalFamily==15||_OriginalFamily==19)
                {
                    float2 secondUV=lerp(i.world,saturate(i.world),_SecondTexClampUV);
                    float4 second=tex2Dbias(_SecondTex,float4(secondUV,0,_GlobalMipBias.x));
                    float2 mainUV=i.uv;
                    if(_OriginalFamily==5)mainUV+=(2*second.rg-1)*_SecondTexSpeed_NoiseForce.zw;
                    mainUV=lerp(mainUV,saturate(mainUV),_ClampUV);
                    float4 original=tex2Dbias(_MainTex,float4(mainUV,0,_GlobalMipBias.x));
                    // Original RGBR PS 1206 takes alpha from red; RGB stays RGB.
                    if(_OriginalFamily==15||_OriginalFamily==19)original.a=original.r;
                    float3 rgb=(original.rgb+_ColorAdd)*max(original.a,_PremultipliedRGBTexture);
                    if(_OriginalFamily==19)return float4(rgb*_ColorMul,original.a)*i.color;
                    float delta=rgb.g-_MiddlePointPos;
                    float3 upper=_MidColor.rgb+(_WhiteColor.rgb-_MidColor.rgb)*(delta/(1-_MiddlePointPos));
                    float3 lower=_LastColor.rgb+(_MidColor.rgb-_LastColor.rgb)*(rgb.g/_MiddlePointPos);
                    float3 ramp=lerp(lower,upper,saturate(delta*5));
                    float mask=_OriginalFamily==6?1+(second.r-1)*_Color2.a:1;
                    return float4(rgb*ramp*_ColorMul,original.a)*mask*i.color;
                }
                if(_OriginalFamily==7)
                {
                    float2 uv=i.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    float4 original=tex2Dbias(_MainTex,float4(uv,0,_GlobalMipBias.x));
                    // Original Multiply PS writes a per-channel destination multiplier.
                    float weight=original.a*i.color.a;
                    return 1+weight*(original*i.color-1);
                }
                if(_OriginalFamily==8||_OriginalFamily==9)
                {
                    float4 effect=tex2Dbias(_FxTex,float4(i.world,0,_GlobalMipBias.x));
                    float4 original=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    return saturate(effect*float4(original.rgb*_Color.rgb,original.a*(_Color.a-original.a))*i.color);
                }
                if(_OriginalFamily==12||_OriginalFamily==13||_OriginalFamily==14)
                {
                    float4 original=tex2Dbias(_MainTex,float4(i.uv,0,_GlobalMipBias.x));
                    float4 effect;
                    if(_OriginalFamily==14)
                    {
                        effect=tex2Dbias(_FxTex1,float4(i.world,0,_GlobalMipBias.x));
                        // Original PS reads TEXCOORD1.yz, not the second pair zw.
                        effect*=tex2Dbias(_FxTex2,float4(i.world.y,i.layerTwo.x,0,_GlobalMipBias.x));
                    }
                    else effect=tex2Dbias(_FxTex,float4(i.world,0,_GlobalMipBias.x));
                    float4 result=effect*float4(original.rgb*_Color.rgb,original.a*(_Color.a-original.a))*i.color;
                    return _OriginalFamily==14?saturate(result):result;
                }
                if(_OriginalFamily==4)
                {
                    float2 baseUV=i.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                    // Original vertex _Time.x is seconds/20. Distortion uses texel size, not a color multiplier.
                    float2 mainUV=baseUV*float2(_ScaleX,_ScaleY)+float2(_ScrollX,_ScrollY)*(_Clock*.05);
                    float2 distortionUV=baseUV*float2(_FxScaleX,_FxScaleY)+float2(_FxScrollX,_FxScrollY)*(_Clock*.05);
                    float2 distortion=(tex2Dbias(_FxTex,float4(distortionUV,0,_GlobalMipBias.x)).rg-.5)*_FxTex_TexelSize.xy*_FxIntensity;
                    float4 original=tex2Dbias(_MainTex,float4(mainUV+distortion,0,_GlobalMipBias.x));
                    return saturate(float4(original.rgb*_Color.rgb,original.a*(_Color.a-original.a))*i.color*(2*_Intensity*_Color.a));
                }
                float2 uv=i.uv;
                if(_HasFx<.5&&abs(_ScrollX)+abs(_ScrollY)>.0001)uv=frac(uv+float2(_ScrollX,_ScrollY)*_Clock);
                // Additive + 1 passes raw UV; Additive and AddMultiply apply ST.
                float2 originalUV=_OriginalFamily==1?uv:uv*_MainTex_ST.xy+_MainTex_ST.zw;
                fixed4 sampleColor=_OriginalFamily>0?tex2Dbias(_MainTex,float4(originalUV,0,_GlobalMipBias.x)):tex2D(_MainTex,originalUV);
                // Original DXBC ps_4_0: alpha = texture.a * (tint.a - texture.a).
                // A RenderToTexture black clear has alpha one and must vanish.
                fixed4 c=_AddMultiply>.5?saturate(float4(sampleColor.rgb*_Color.rgb,sampleColor.a*(_Color.a-sampleColor.a))*i.color):sampleColor*_Color*i.color;
                if(_OriginalFamily>0)
                {
                    if(_OriginalFamily==1)c=sampleColor*_Color*i.color*_ColorMul;
                    else if(_OriginalFamily==2)c=sampleColor*_Color*i.color*2;
                    else c=saturate(float4(sampleColor.rgb*_Color.rgb,sampleColor.a*(_Color.a-sampleColor.a))*i.color*(2*_Intensity));
                    c.a=saturate(c.a*lerp(1,max(c.r,max(c.g,c.b)),_LuminanceAlpha));return c;
                }
                c*=lerp(float4(1,1,1,1),_TintColor,_ParticleTint);
                float2 fxuv=lerp(i.uv-.5,i.world,_FxWorld)*float2(_ScaleX,_ScaleY)+lerp(.5,0,_FxWorld)+float2(_ScrollX,_ScrollY)*_Clock;
                fxuv=frac(fxuv*_FxTex_ST.xy+_FxTex_ST.zw);
                fixed4 fx=tex2D(_FxTex,fxuv)*_FxColor;
                c=lerp(c,c*fx*_FxIntensity,_HasFx);
                if(_TwoLayer>.5)c*=tex2D(_MainTex,(i.uv-.5)*_Scale+.5+float2(_USpeed,_VSpeed)*_Clock);
                c.rgb*=_Intensity*_ColorMul;
                float luminance=max(c.r,max(c.g,c.b));
                c.a=saturate(c.a*_AlphaIntensity*lerp(1,luminance,_LuminanceAlpha));return c;
            }
            ENDCG
        }
    }
}
