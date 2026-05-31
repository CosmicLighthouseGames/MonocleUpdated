#include "../material_header.fxh"

BasicMaterialHeader()
BoneMatrices()

DECLARE_TEXTURE(Texture, 0, Point, Clamp)

#define BasicVSStructs()
struct VertexInput
{
    MonocleVertexInput()
    MonocleBoneWeights()
};
struct VertexOutput
{
    MonocleVertexOutput()
};

#define BasicPSStruct()
struct ShaderOutput
{
    FragmentTarget(color, 0)
    FragmentTarget(normal, 1)
    float depth : SV_Depth;
};

VertexOutput VertexShader_Main(VertexInput input)
{
    VertexOutput output;
    
    output.position = WorldPositionSkinned(input.position, input);
    output.texCoord = MappedTexCoord(input.texCoord, Texture);
    output.normal = WorldNormal(input.normal);
    output.color = input.color * DiffuseColor;
    output.depthBuffer = output.position.zw;
	
    return output;
}
ShaderOutput PixelShader_Main(VertexOutput input)
{
    ShaderOutput retval;
	
    float4 texColor = tex2D(TextureSampler, input.texCoord);
    texColor *= input.color;
	
    bool visible = texColor.a > 0;
	
    retval.color = texColor * visible;
    retval.normal = float4(input.normal / 2 + 0.5, 1) * visible;
	
    retval.depth = input.depthBuffer.r * visible;

	
    return retval;
}

MaterialTechniqueStart(Opaque)
MaterialPass(VertexShader_Main, PixelShader_Main)
MaterialTechniqueEnd()
