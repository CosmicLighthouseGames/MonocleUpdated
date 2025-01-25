using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public delegate void TransformVertex(ref MonocleVertex vertex);
	public unsafe class DynamicModelRenderCall : IDrawCall {
		static MonocleVertex[] buffer = new MonocleVertex[0x2000];

		public Material material;
		public MonocleVertex[] vertices;
		public short[] indices;
		public Matrix transform;
		public List<TransformVertex> modifiers;
		public DepthStencilState DepthStencilState;

		public int RenderOrder { get; set; }

		public DynamicModelRenderCall() {

		}


		public void Render(GraphicsDevice device) {


			fixed (MonocleVertex* meshPtr = vertices) {
				fixed (MonocleVertex* bufferPtr = buffer) {
					for (int i = 0; i < vertices.Length; i++) {
						var vert = meshPtr[i];

						foreach (var t in modifiers) {
							t(ref vert);
						}

						bufferPtr[i] = vert;
					}
				}
			}

			Material mat = null;
			var drawcall = this;

			void SetMaterial(Material newMat) {
				if (mat == newMat) {
					return;
				}
				mat = newMat;

				var tech = mat.Technique;
				var techPass = tech.Passes[0];

				var stencil = DepthStencilState??mat.DepthStencilState??Draw.DefaultDepthState;
				if (stencil != device.DepthStencilState) {
					device.DepthStencilState = stencil;
				}

				var tex = mat.Texture??Draw.Pixel;
				var pData = mat.parameterData;

				Draw.SetParameters(mat.BaseEffect, (param) => {
					switch (param.Name) {
						case "DiffuseColor":
							param.SetValue(mat.Color.ToVector4());
							return true;
						case "Texture":
							param.SetValue(tex.Texture);
							return true;
						case "World":
							param.SetValue(drawcall.transform);
							return true;
						default:
							if (pData.ContainsKey(param.Name)) {
								var data = pData[param.Name];
								if (data is MTexture)
									param.SetValue(data.Texture);
								else if (data is Color)
									param.SetValue(data.ToVector4());
								else
									param.SetValue(pData[param.Name]);
								return true;
							}
							return false;
					}
				});

				techPass.Apply();
			}

			Material newMat;
			if (Draw.OverridingMaterial != null) {
				newMat = Draw.OverridingMaterial;
			}
			else {
				newMat = material;
			}
			SetMaterial(newMat);
			device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, buffer, 0, vertices.Length, indices, 0, indices.Length / 3);
			
		}
	}
	public class DynamicModelRenderer : BasicModelRenderer {


		public DynamicModelRenderer() : base() {

		}

		public List<TransformVertex> Transforms = new List<TransformVertex>();

		public override void Render() {

			if (Mesh == null)
				return;

			for (int i = 0; i < Mesh.indices.Length; i++) {
				var mat = materials.Length > 0 ? ((i < materials.Length) ? materials[i] : materials[0]) : Draw.DefaultMaterial;
				Draw.CustomDrawCall(new DynamicModelRenderCall() {
					vertices = Mesh.vertices,
					indices = Mesh.indices[i],
					material = mat,
					modifiers = Transforms,
					transform = TransMatrix(),
					RenderOrder = mat.RenderOrder??Draw.CurrentRenderOrder,
				});
			}


		}
	}
}
