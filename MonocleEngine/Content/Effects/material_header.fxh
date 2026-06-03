
//-----------------------------------------------------------------------------
// Macros.fxh + Custom macros 
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

#if defined(SM6) || defined(VULKAN)

#define BEGIN_CONSTANTS     cbuffer _MG_Globals : register(b0) {
#define MATRIX_CONSTANTS
#define END_CONSTANTS       };

#define _vs(r)
#define _ps(r)
#define _cb(r)

#define DECLARE_TEXTURE(Name, index, filter, loop) \
	Texture2D<float4> Name : register(t##index); \
	float4 Name##_Clip;                     \
	float2 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_TEXTURE3D(Name, index, filter, loop)\
	Texture3D<float4> Name : register(t##index);    \
	float4 Name##_Clip;                             \
	float3 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_CUBEMAP(Name, index, filter, loop) \
	TextureCube<float4> Name : register(t##index); \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define SAMPLE_TEXTURE(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)
#define SAMPLE_CUBEMAP(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)

#define UNROLL [unroll]

#elif defined(SM4)

// Macros for targetting shader model 4.0 (DX11)

#define BEGIN_CONSTANTS     cbuffer Parameters : register(b0) {
#define MATRIX_CONSTANTS
#define END_CONSTANTS       };

#define _vs(r)
#define _ps(r)
#define _cb(r)

#define DECLARE_TEXTURE(Name, index, filter, loop) \
	Texture2D<float4> Name : register(t##index); \
	float4 Name##_Clip;                     \
	float2 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_TEXTURE3D(Name, index, filter, loop) \
	Texture3D<float4> Name : register(t##index); \
	float4 Name##_Clip;                     \
	float3 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_CUBEMAP(Name, index, filter, loop) \
	TextureCube<float4> Name : register(t##index); \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define SAMPLE_TEXTURE(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)
#define SAMPLE_CUBEMAP(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)

#define UNROLL [unroll]

#else


// Macros for targetting shader model 2.0 (DX9)

#define BEGIN_CONSTANTS
#define MATRIX_CONSTANTS
#define END_CONSTANTS

#define _vs(r)  : register(vs, r)
#define _ps(r)  : register(ps, r)
#define _cb(r)


#define DECLARE_TEXTURE(Name, index, filter, loop) \
	Texture2D<float4> Name : register(t##index); \
	float4 Name##_Clip;                     \
	float2 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_TEXTURE3D(Name, index, filter, loop) \
	Texture3D<float4> Name : register(t##index); \
	float4 Name##_Clip;                     \
	float3 Name##_Size;                     \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define DECLARE_CUBEMAP(Name, index, filter, loop) \
	TextureCube<float4> Name : register(t##index); \
	sampler Name##Sampler : register(s##index)\
{                                       \
	Texture = ( Name );                 \
	MagFilter = filter;\
	MinFilter = filter;\
	AddressU = loop;\
	AddressV = loop;\
};                                      \

#define SAMPLE_TEXTURE(Name, texCoord)  tex2D(Name, texCoord)
#define SAMPLE_CUBEMAP(Name, texCoord)  texCUBE(Name, texCoord)

#define UNROLL [unroll]

#endif


#define BasicMaterialHeader()						\
	float4 DiffuseColor;							\
	float4x4 World : register(vs, c0);				\
	float4x4 WorldViewProj : register(vs, c4);		

#define BoneMatrices()							\
float4x4 Bone0 : register(vs, c8);				\
float4x4 Bone1 : register(vs, c12);				\
float4x4 Bone2 : register(vs, c16);				\
float4x4 Bone3 : register(vs, c20);

#define MonocleVertexInput()			\
	float4 position : SV_Position;		\
	float4 color : COLOR0;				\
	float4 normal : NORMAL0;			\
	float2 texCoord : TEXCOORD0;		\

#define MonocleBoneWeights()			\
	float weight0 : BLENDWEIGHT0;		\
	float weight1 : BLENDWEIGHT1;		\
	float weight2 : BLENDWEIGHT2;		\
	float weight3 : BLENDWEIGHT3;

#define MonocleVertexOutput()					\
	float4 position : SV_Position;				\
	float4 color : COLOR0;						\
	float2 texCoord : TEXCOORD0;				\
	float2 depthBuffer : TEXCOORD1;				\
	float3 normal : TEXCOORD2;

#define FragmentTarget(color, c)		\
	float4 color : SV_Target##c;		\

#define WorldPosition(position) mul(mul(position, World), WorldViewProj)
#define WorldPositionSkinned(position, input) mul(mul(mul(position, lerp(lerp(Bone0, Bone1, input.weight1 / max(input.weight0 + input.weight1, 0.001)),lerp(Bone2, Bone3, input.weight3 / max(input.weight2 + input.weight3, 0.001)), input.weight2 + input.weight3)), World), WorldViewProj)

#define WorldNormal(normal) normalize(mul(normal.xyz, (float3x4)World))
#define ScreenNormal(normal) normalize(mul(mul(normal.xyz, (float3x4)World), (float3x4)WorldViewProj))

#define MappedTexCoord(texCoord, texture)	(texCoord * texture##_Clip.xy) + texture##_Clip.zw;


#define TECHNIQUE(name, vsname, psname ) \
	technique name { pass { VertexShader = compile vs_6_0 vsname (); PixelShader = compile ps_6_0 psname(); } }

#define MaterialTechniqueStart(techname) technique techname {


#if defined(SM6) || defined(VULKAN)

#define MaterialPass(vertexshader, pixelshader)\
pass\
{\
	VertexShader = compile vs_6_0 vertexshader();\
	PixelShader = compile ps_6_0 pixelshader();\
}\


#elif defined(SM4)

#define MaterialPass(vertexshader, pixelshader)\
pass\
{\
	VertexShader = compile vs_4_0_level_9_1 vertexshader();\
	PixelShader = compile ps_4_0_level_9_1 pixelshader();\
}\

#else

#define MaterialPass(vertexshader, pixelshader)\
pass\
{\
	VertexShader = compile vs_3_0 vertexshader();\
	PixelShader = compile ps_3_0 pixelshader();\
}\

#endif


#define MaterialTechniqueEnd() }

