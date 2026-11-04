// Recovered from UnlitTexture DXBC blobs 1, 5 and 7 (D3D11).
float4 OriginalFrameShadow(float3 world,float3 normal,float3 direction,float2 bias,float4x4 vp)
{
    normal=normalize(normal);
    world+=direction*bias.x+normal*((1-saturate(dot(direction,normal)))*bias.y);
    float4 clip=mul(vp,float4(world,1));
    clip.z=min(clip.w,clip.z);
    return clip;
}
float4 OriginalFrameColor(float4 vertex,float3 ambient,float3 mainLight,float blend)
{
    float3 lighting=saturate(1.055*pow(abs(ambient),1.0/2.4)-.055)+mainLight;
    return lerp(vertex,float4(lighting,1),blend);
}
