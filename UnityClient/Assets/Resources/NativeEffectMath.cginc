float3 OriginalParticleLighting(float3 shConstant,float3 mainLight,float blend)
{
    // Original vertex DXBC uses absolute SH L0, gamma conversion and saturation.
    float3 gamma=saturate(1.055*pow(abs(shConstant),.416667)-.055);
    return 1+blend*(gamma+mainLight-1);
}
