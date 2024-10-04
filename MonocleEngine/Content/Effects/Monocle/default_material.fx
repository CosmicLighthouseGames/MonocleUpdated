Texture2D<float4> Texture : register(t0);
sampler Texture_Sampler = sampler_state {
	Texture = (Texture);
	MagFilter = Point;
	MinFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

float4 DiffuseColor;
float4x4 World : register(vs, c0);
float4x4 WorldViewProj : register(vs, c4);

struct VSInput
{
	float4 position		: SV_Position;
	float4 color		: COLOR0;
    float2 texCoord		: TEXCOORD0;
};

struct VSOutput
{
	float4 position		: SV_Position;
	float4 color		: COLOR0;
    float2 texCoord		: TEXCOORD0;
    float2 depthBuffer	: TEXCOORD1;
};

struct PSOutput
{
	float4 color		: SV_Target0;
	float depth			: SV_Depth;
};

VSOutput VertexShader_Main (VSInput input)
{
	VSOutput output;
	
	float4 position = input.position;
	position = mul(position, World);
	position = mul(position, WorldViewProj);
	output.position = position;
    output.depthBuffer = float2(position[2], 0);
	
	float2 texCoord = input.texCoord;
	output.texCoord = texCoord;

    output.color = input.color;
	
	return output;
}
PSOutput PixelShader_Main(VSOutput input)
{
	PSOutput retval;
	
	float4 c = tex2D(Texture_Sampler, input.texCoord) * DiffuseColor * input.color;
	
	if (c.a >= 0.0001)
		retval.depth = input.depthBuffer[0];
	else
		retval.depth = 1;
	
	retval.color = c;
	
	
	return retval;
}

technique Opaque
{
	pass
	{
#if SM4
        VertexShader = compile vs_4_0_level_9_1 VertexShader_Main();
        PixelShader = compile ps_4_0_level_9_1 PixelShader_Main();
#else
        VertexShader = compile vs_3_0 VertexShader_Main();
        PixelShader = compile ps_3_0 PixelShader_Main();
#endif
	}
}