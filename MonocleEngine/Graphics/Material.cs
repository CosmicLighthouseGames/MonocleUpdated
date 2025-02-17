using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle {
	public class Material {

		static Dictionary<string, Effect> effects = new Dictionary<string, Effect>();

		public static void Initialize() {
			var gd = Draw.GraphicsDevice;

			foreach (var content in AssetLoader.GetContentInFolder("Effects")) {
				string localPath = content.Path.Substring(8, content.Path.IndexOf('.') - 8).Replace('\\', '/');

				effects.Add(localPath, new Effect(gd, content.GetBinary()));
			}
		}

		public static IEnumerable<Effect> LoadedEffects() {
			return effects.Values;
		}
		public static Effect GetEffect(string name) {
			if (effects.ContainsKey(name)) { return effects[name]; }
			return null;
		}


		public static Material DefaultMaterial(MTexture tex) {
			return new Material() {
				Color = Color.White,
				Texture = tex,
			};
		}
		public static Material DefaultMaterial(MTexture tex, Color color) {
			return new Material() {
				Color = color,
				Texture = tex,
			};
		}
		public static Material DefaultMaterial(Color color) {
			return new Material() {
				Color = color,
				Texture = Draw.Pixel,
			};
		}

		public static Material FromEffect(string name) {
			return new Material(name);
		}

		public Material() {
			BaseEffect = Draw.DefaultEffect;
			Technique = BaseEffect.CurrentTechnique;
			Color = Color.White;
			Name = "Default Material";
		}
		public Material(string name) {
			if (!effects.ContainsKey(name))
				throw new Exception();
			BaseEffect = GetEffect(name);
			Technique = BaseEffect.CurrentTechnique;
			Color = Color.White;
			Name = name;
		}
		public Material(Material other) {
			BaseEffect = other.BaseEffect;
			Technique = other.Technique;
			Color = other.Color;
			Name = other.Name;
			Texture = other.Texture;
			DepthStencilState = other.DepthStencilState;
			foreach (var param in other.parameterData) {
				parameterData[param.Key] = param.Value;
			}
		}

		public string Name { get; private set; }

		public Effect BaseEffect { get; private set; }
		public EffectTechnique Technique { get; private set; }

		public Color Color;
		public MTexture Texture;
		public DepthStencilState DepthStencilState;

		public int? Stencil {
			get {
				if (DepthStencilState == null)
					return null;
				return DepthStencilState.ReferenceStencil;
			}
			set {
				if (value == null)
					return;

				if (DepthStencilState == null) {
					DepthStencilState = new DepthStencilState();
					DepthStencilState.ReadFrom(Draw.DefaultDepthState);

					DepthStencilState.StencilEnable = true;
					DepthStencilState.StencilWriteMask = int.MaxValue;
					DepthStencilState.StencilMask = int.MaxValue;
					DepthStencilState.StencilFunction = CompareFunction.Always;
					DepthStencilState.CounterClockwiseStencilFunction = CompareFunction.Always;
					DepthStencilState.StencilPass = StencilOperation.Replace;

				}
				DepthStencilState.ReferenceStencil = value.Value;
				DepthStencilState.StencilWriteMask = value.Value;
			}
		}
		public int? StencilMask {
			get {
				if (DepthStencilState == null)
					return null;
				return DepthStencilState.StencilMask;
			}
			set {
				if (value == null)
					return;

				if (DepthStencilState == null) {
					DepthStencilState = new DepthStencilState();
					DepthStencilState.ReadFrom(Draw.DefaultDepthState);
				}
				DepthStencilState.StencilMask = value.Value;
			}
		}

		public int? RenderOrder = null;

		public Dictionary<string, dynamic> parameterData = new Dictionary<string, dynamic>();

		public Material SetTechnique(string technique) {
			var t = BaseEffect.Techniques[technique];
			if (t != null) {
				Technique = t;
			}
			return this;
		}

		public Material SetParameter(string name, object value) {
			parameterData[name] = value;
			return this;
		}
		public Material SetTexture(MTexture texture) {
			Texture = texture;
			return this;
		}
		public Material SetColor(Color color) {
			Color = color;
			return this;
		}
		public Material SetStencil(int stencil) {

			Stencil = stencil;
			StencilMask = stencil;
			return this;
		}
		public Material SetRenderOrder(int? renderPass) {
			RenderOrder = renderPass;
			return this;
		}
	}
}
