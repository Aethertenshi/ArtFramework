// Textures and Samplers
sampler SpriteTextureSampler : register(s0);

// Parameters from C#
float2 atlasSize;
float pxRange;

struct VertexShaderOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

// The median function is the heart of MSDF
float median(float r, float g, float b) {
    return max(min(r, g), min(max(r, g), b));
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float4 sample = tex2D(SpriteTextureSampler, input.TexCoord);
    float sigDist = median(sample.r, sample.g, sample.b);

    float2 unitRange = float2(pxRange, pxRange) / atlasSize;
    float2 screenTexSize = float2(1.0, 1.0) / fwidth(input.TexCoord);
    float screenPxRange = max(0.5 * dot(unitRange, screenTexSize), 1.0);

    float opacity = clamp(sigDist * screenPxRange + 0.5 - 0.5 * screenPxRange, 0.0, 1.0);

    // Discard near-transparent fragments so atlas distance data doesn't bleed through
    clip(opacity - 0.001);

    return float4(input.Color.rgb, input.Color.a * opacity);
}

technique MtsdfRendering
{
    pass P0
    {
        // Change from ps_3_0 to ps_4_0_level_9_1
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
};