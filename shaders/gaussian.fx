#define KERNEL_SIZE 15

// Parameters provided by your C# code
float4x4 MatrixTransform;
float2 SampleOffsets[KERNEL_SIZE];
float SampleWeights[KERNEL_SIZE];

texture ScreenTexture;
sampler TextureSampler = sampler_state
{
	Texture = <ScreenTexture>;
	AddressU = Clamp;
	AddressV = Clamp;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Point;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_Position;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};

// Standard SpriteBatch Vertex Shader
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = mul(input.Position, MatrixTransform);
	output.Color = input.Color;
	output.TexCoord = input.TexCoord;
	return output;
}

// Gaussian Blur Pixel Shader
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float4 color = float4(0.0f, 0.0f, 0.0f, 0.0f);

    // Accumulate the color based on the Gaussian kernel
	for (int i = 0; i < KERNEL_SIZE; ++i)
	{
		color += tex2D(TextureSampler, input.TexCoord + SampleOffsets[i]) * SampleWeights[i];
	}

    // Multiply by the vertex color to maintain SpriteBatch tinting/alpha compatibility
	return color * input.Color;
}

technique GaussianBlur
{
	pass Pass1
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PixelShaderFunction();
	}
}