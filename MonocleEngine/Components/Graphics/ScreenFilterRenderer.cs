using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public class FilterCall : IDrawCall {
		static MonocleVertex[] vertices = new MonocleVertex[4] {
			new MonocleVertex() { Position = new Vector3(-1, -1, 0.0f), TextureCoordinate = new Vector2(0, 1), Color = Vector4.One },
			new MonocleVertex() { Position = new Vector3(-1, 1, 0.0f), TextureCoordinate = new Vector2(0, 0), Color = Vector4.One },
			new MonocleVertex() { Position = new Vector3(1, -1, 0.0f), TextureCoordinate = new Vector2(1, 1), Color = Vector4.One },
			new MonocleVertex() { Position = new Vector3(1, 1, 0.0f), TextureCoordinate = new Vector2(1, 0), Color = Vector4.One },
		};
		static short[] indices = new short[]{
			0, 1, 2, 3, 2, 1
		};
		static Effect Vertex;

		public static void Initialize() {
			Vertex = Material.GetEffect("Monocle/filter_vertex");
		}

		public ScreenFilter[] Filters;
		public Action? AfterRender;

		public void Render(GraphicsDevice device) {

			var dState = device.DepthStencilState;
			var bState = device.BlendState;
			var rState = device.RasterizerState;

			var worldProj = Draw.WorldProjection;
			Draw.WorldProjection = Matrix.Identity;

			device.DepthStencilState = DepthStencilState.None;
			device.RasterizerState = RasterizerState.CullNone;

			Vertex.CurrentTechnique.Passes[0].Apply();
			var targets = device.GetRenderTargets();



			foreach (var mat in Filters) {

				if (mat.renderTargets != null)
					device.SetRenderTargets(mat.renderTargets);

				device.BlendState = mat.blendState??BlendState.AlphaBlend;

				var material = mat.material;
				var tech = material.Technique;
				var techPass = tech.Passes[0];

				if (material.Stencil != device.DepthStencilState.ReferenceStencil) {
					var dsMask = new DepthStencilState();
					dsMask.ReadFrom(device.DepthStencilState);
					dsMask.ReferenceStencil = material.Stencil;
					device.DepthStencilState = dsMask;
				}

				var tex = material.Texture??Draw.Pixel;
				var drawcall = this;
				var pData = material.parameterData;

				Draw.SetParameters(material.BaseEffect, (param) => {
					switch (param.Name) {
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
				device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 4, indices, 0, 2);
			}

			device.DepthStencilState = dState;
			device.BlendState = bState;
			device.RasterizerState = rState;
			Draw.WorldProjection = worldProj;

			device.SetRenderTargets(targets);

			AfterRender?.Invoke();
		}

	}
	public class ScreenFilter {
		public string Name => material.Name;
		public Material material;
		public BlendState blendState;
		internal RenderTargetBinding[] renderTargets;

		public ScreenFilter SetMaterial(string name) {
			this.material = Material.FromEffect(name);
			return this;
		}
		public ScreenFilter SetMaterial(Material material) {
			this.material = material;
			return this;
		}
		public ScreenFilter SetBlendState(BlendState blendState) {
			this.blendState = blendState;
			return this;
		}
		public ScreenFilter SetRenderTargets(RenderTarget2D[] renderTargets) {
			this.renderTargets = renderTargets.Select((a) => { return (RenderTargetBinding)a; }).ToArray();
			return this;
		}
	}
	public class ScreenFilterRenderer : GraphicsComponent {

		public List<ScreenFilter> Filters = new List<ScreenFilter>();

		public Action AfterRender;

		public ScreenFilterRenderer(int RenderOrder) : base(true) {
			this.RenderOrder = RenderOrder;
		}

		public override void Render() {
			base.Render();

			Draw.CustomDrawCall(new FilterCall() {
				Filters = Filters.ToArray(),
				AfterRender = AfterRender,
			});

		}
	}
}
