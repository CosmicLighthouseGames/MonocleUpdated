using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public class FilterCall : IDrawCall {


		public static void ChangeUV(RectangleF coords) {
		}


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

			Draw.SpriteDrawCall.SetBuffers();

			foreach (var filter in Filters) {
				if (!filter.Active)
					continue;


				if (filter.renderTargets != null) {
					var rendertargets = device.GetRenderTargets();
					if (rendertargets.Length != filter.renderTargets.Length || Enumerable.SequenceEqual(rendertargets, filter.renderTargets)) {
						device.SetRenderTargets(filter.renderTargets);
					}
				}

				var material = filter.material;
				var tech = material.GetTechnique(RenderOrder);


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


				device.BlendState = filter.blendState??BlendState.AlphaBlend;
				device.DepthStencilState = filter.depthStencilState;

				device.Viewport = viewport;

				foreach (var pass in tech.Passes) {
					pass.Apply();
					Draw.SpriteDrawCall.RenderSprite();
				}
			}


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
