using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public class FilterCall : IDrawCall {

		static VertexBuffer mesh;
		static IndexBuffer indices;

		public static void SetBuffers(GraphicsDevice device) {
			device.SetVertexBuffer(mesh);
			device.Indices = indices;
		}

		public static void Initialize() {
			mesh = new VertexBuffer(Draw.GraphicsDevice, typeof(MonocleVertex), 4, BufferUsage.WriteOnly);
			mesh.SetData(new MonocleVertex[4] {
					new MonocleVertex() {
						Position = new Vector3(-1, -1, 0),
						TextureCoordinate = new Vector2(0, 1),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
					new MonocleVertex() {
						Position = new Vector3(1, -1, 0),
						TextureCoordinate = new Vector2(1, 1),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
					new MonocleVertex() {
						Position = new Vector3(-1, 1, 0),
						TextureCoordinate = new Vector2(0, 0),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
					new MonocleVertex() {
						Position = new Vector3(1, 1, 0),
						TextureCoordinate = new Vector2(1, 0),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
				});
			indices = new IndexBuffer(Draw.GraphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
			indices.SetData(new short[]{
				1, 2, 3, 2, 1, 0
			});
			Vertex = Material.GetEffect("Monocle/filter_vertex");
		}

		public static void ChangeUV(RectangleF coords) {

			//vertices[0].TextureCoordinate = new Vector2(coords.Left, coords.Bottom);
			//vertices[1].TextureCoordinate = new Vector2(coords.Left, coords.Top);
			//vertices[2].TextureCoordinate = new Vector2(coords.Right, coords.Bottom);
			//vertices[3].TextureCoordinate = new Vector2(coords.Right, coords.Top);
		}
		//static MonocleVertex[] vertices = new MonocleVertex[4] {
		//	new MonocleVertex() { Position = new Vector3(-1, -1, 0.0f), TextureCoordinate = new Vector2(0, 1), Color = Vector4.One },
		//	new MonocleVertex() { Position = new Vector3(-1, 1, 0.0f), TextureCoordinate = new Vector2(0, 0), Color = Vector4.One },
		//	new MonocleVertex() { Position = new Vector3(1, -1, 0.0f), TextureCoordinate = new Vector2(1, 1), Color = Vector4.One },
		//	new MonocleVertex() { Position = new Vector3(1, 1, 0.0f), TextureCoordinate = new Vector2(1, 0), Color = Vector4.One },
		//};
		//static short[] indices = new short[]{
		//	0, 1, 2, 3, 2, 1
		//};
		static Effect Vertex;


		public ScreenFilter[] Filters;
		public Action BeforeRender, AfterRender;

		public int RenderOrder { get; set; }

		public void Render(GraphicsDevice device) {


			
			BeforeRender?.Invoke();

			var bState = device.BlendState;
			var rState = device.RasterizerState;
			var dsState = device.DepthStencilState;
			var viewport = device.Viewport;

			var worldProj = Draw.WorldProjection;
			Draw.WorldProjection = Matrix.Identity;

			device.RasterizerState = RasterizerState.CullNone;

			var targets = device.GetRenderTargets();

			device.SetVertexBuffer(mesh);
			device.Indices = indices;

			foreach (var filter in Filters) {

				device.Reset();

				if (!filter.Active)
					continue;

				if (filter.renderTargets != null) {
					var rendertargets = device.GetRenderTargets();
					if (rendertargets.Length != filter.renderTargets.Length || Enumerable.SequenceEqual(rendertargets, filter.renderTargets)) {
						device.SetRenderTargets(filter.renderTargets);
					}
				}

				var material = filter.material;
				var tech = material.Technique;
				var techPass = tech.Passes[0];


				var tex = material.Texture??Draw.Pixel;
				var drawcall = this;
				var pData = material.parameterData;

				Draw.SetParameters(material.BaseEffect, (param, effect) => {
					switch (param.Name) {
						default:
							if (pData.ContainsKey(param.Name)) {
								var data = pData[param.Name];
								if (data is MTexture)
									effect.SetParameter(param.Name, data as MTexture);
								else if (data is Color)
									param.SetValue(data.ToVector4());
								else if (param.ParameterType == EffectParameterType.Int32)
									param.SetValue((int)pData[param.Name]);
								else
									param.SetValue(pData[param.Name]);
								return true;
							}
							return false;
					}
				});

				Vertex.CurrentTechnique.Passes[0].Apply();
				techPass.Apply();

				device.BlendState = filter.blendState??BlendState.AlphaBlend;
				device.DepthStencilState = filter.depthStencilState;

				device.Viewport = viewport;
				device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
			}

			device.Reset();

			device.BlendState = bState;
			device.RasterizerState = rState;
			device.DepthStencilState = dsState;
			Draw.WorldProjection = worldProj;

			device.SetRenderTargets(targets);
			device.Viewport = viewport;

			AfterRender?.Invoke();
		}

	}
	public class ScreenFilter {
		public string Name => material.Name;
		public Material material;
		public BlendState blendState;
		public DepthStencilState depthStencilState = new DepthStencilState(){
			StencilMask = 0,
			Name = "NoneMonocle",
			DepthBufferEnable = false,
			DepthBufferWriteEnable = false,
			StencilEnable = false,
		};
		internal RenderTargetBinding[] renderTargets;
		public bool Active = true;

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
		public ScreenFilter SetDepthStencilState(DepthStencilState depthStencilState) {
			this.depthStencilState = depthStencilState;
			return this;
		}
		public ScreenFilter SetRenderTargets(params RenderTarget2D[] renderTargets) {
			this.renderTargets = renderTargets.Select((a) => { return (RenderTargetBinding)a; }).ToArray();
			return this;
		}
	}
	public class ScreenFilterRenderer : GraphicsComponent {

		public List<ScreenFilter> Filters = new List<ScreenFilter>();

		public Action BeforeRender, AfterRender;

		public ScreenFilterRenderer(int RenderOrder) : base(true) {
			this.RenderOrder = RenderOrder;
		}

		public override void Render() {
			base.Render();

			Draw.CustomDrawCall(new FilterCall() {
				Filters = Filters.ToArray(),
				BeforeRender = BeforeRender,
				AfterRender = AfterRender,
				RenderOrder = RenderOrder,
			});

		}
	}
}
