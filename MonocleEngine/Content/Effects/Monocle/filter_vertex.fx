struct VSInput
{
	float4 position		: SV_Position;
    float2 texCoord		: TEXCOORD0;
};

struct VSOutput
{
	float4 position		: SV_Position;
    float2 texCoord		: TEXCOORD0;
};

VSOutput VertexShader_Main (VSInput input)
{
	VSOutput output;
	
	float4 position = input.position;
	output.position = position;
	
	float2 texCoord = input.texCoord;
	output.texCoord = texCoord;
	
	return output;
}

technique Opaque
{
	pass
	{
#if SM4
        VertexShader = compile vs_4_0_level_9_1 VertexShader_Main();
#else
        VertexShader = compile vs_3_0 VertexShader_Main();
#endif
	}
}