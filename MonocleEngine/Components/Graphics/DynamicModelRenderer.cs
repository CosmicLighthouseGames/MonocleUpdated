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

		public Material material {
			set {
				materials = new Material[] { value };
			}
		}
		public MonocleVertex[] vertices;
		public short[][] indices;
		public Material[] materials;
		public Matrix transform;
		public List<TransformVertex> transforms;


		public void Render(GraphicsDevice device) {


			fixed (MonocleVertex* meshPtr = vertices) {
				fixed (MonocleVertex* bufferPtr = buffer) {
					for (int i = 0; i < vertices.Length; i++) {
						var vert = meshPtr[i];

						foreach (var t in transforms) {
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

				if (mat.Stencil != device.DepthStencilState.ReferenceStencil) {
					var dsMask = new DepthStencilState();
					dsMask.ReadFrom(device.DepthStencilState);
					dsMask.ReferenceStencil = mat.Stencil;
					device.DepthStencilState = dsMask;
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

			for (int i = 0; i < indices.Length; i++) {
				Material newMat;
				if (Draw.OverridingMaterial != null) {
					newMat = Draw.OverridingMaterial;
				}
				else if (i >= materials.Length) {
					newMat = materials[0];
				}
				else {
					newMat = materials[i];
				}
				SetMaterial(newMat);
				device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, buffer, 0, vertices.Length, indices[i], 0, indices[i].Length / 3);
			}
		}
	}
	public class DynamicModelRenderer : BasicModelRenderer {


		public DynamicModelRenderer() : base() {

		}

		public List<TransformVertex> Transforms = new List<TransformVertex>();

		public override void Render() {

			if (Mesh == null)
				return;

			Vector3 position = Position;

			if (Entity != null) {
				position += Entity.Position;
			}

			Draw.CustomDrawCall(new DynamicModelRenderCall() {
				vertices = Mesh.vertices,
				indices = Mesh.indices,
				material = Material??Draw.DefaultMaterial,
				transforms = Transforms,
				transform = TransMatrix()
			});

		}
	}
}