using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle {
	public class Material {

		static Dictionary<string, Effect> effects = new Dictionary<string, Effect>();

		
		public static void Initialize() {
			var gd = Draw.GraphicsDevice;

			Directory.CreateDirectory("tmp");

			using Process cmd = new Process();
			ProcessStartInfo info = new ProcessStartInfo();
			info.FileName = "cmd.exe";
			info.WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory());
			info.RedirectStandardInput = true;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.CreateNoWindow = true;

			cmd.StartInfo = info;
			cmd.Start();
			
			using StreamWriter compiler = cmd.StandardInput;


			using var se = cmd.StandardError;
			using var so = cmd.StandardOutput;
			bool error = false;
			Task.Factory.StartNew(() => {

				while (!se.EndOfStream) {
					var line = se.ReadLine();

					if (Regex.IsMatch(line, @"effect\.fx\([\d-,]+\): error")) {
						error = true;
					}
				}
			});
			Task.Factory.StartNew(() => {

				while (!so.EndOfStream) {
					var line = so.ReadLine();
				}
			});

			DebugLog.Write($"Loading effects");

			foreach (var content in AssetLoader.GetContentInFolder("Effects")) {
				string localPath = content.Path.Substring(8, content.Path.IndexOf('.') - 8).Replace('\\', '/');
				if (content.Extention == ".fx") {

					File.Delete("tmp/compiled.cso");
					var comp = AssetLoader.GetContent(Path.ChangeExtension(content.Path, ".cso"));

					if (comp == null || comp.LastEdit < content.LastEdit) {

						using (StreamReader sr = new StreamReader(content.ContentStream)) {
							using (StreamWriter sw = new StreamWriter(File.Open("tmp/effect.fx", FileMode.Create))) {
								sw.Write(sr.ReadToEnd());
							}
						}

						error = false;
						compiler.WriteLine(@$"mgfxc ""tmp/effect.fx"" ""tmp/compiled.cso""");

						//var line = se.ReadLine()!;

						for (int i = 0; i < 200 && !File.Exists("tmp/compiled.cso") && !error; i++) {
							Thread.Sleep(50);
						}

						if (File.Exists("tmp/compiled.cso")) {

							for (int i = 0; i < 20; i++) {
								try {
									effects.Add(localPath, new Effect(gd, File.ReadAllBytes("tmp/compiled.cso")));
									File.Move("tmp/compiled.cso", $"Content/Effects/{localPath}.cso", true);
									break;
								}
								catch {
									Thread.Sleep(50);
								}
							}
							
						}
						else {
							ErrorLog.Write($"Error compiling {localPath}");
						}
					}


				}
				else if (content.Extention == ".cso") {


					DebugLog.Write($"Loading effect {content.Path}");

					if (!effects.ContainsKey(localPath)) {

						try {
							effects.Add(localPath, new Effect(gd, content.GetBinary()));
						}
						catch {
							ErrorLog.Write($"Error loading effect: {content.Path}");
						}
					}
				}

			}


			cmd.Close();
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
				throw new Exception($"Missing {name} Material");
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
		public MTexture Texture {
			get {
				if (TextureImage != null)
					return TextureImage.Texture;
				return _tex;
			}
			set {
				_tex = value;
			}
		}
		MTexture _tex;
		public Image TextureImage;
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
		public Material SetTexture(Image image) {
			TextureImage = image;
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

		public void SetParameters(Matrix worldTransform, MTexture overrideTexture, Color? offsetColor = null, SpriteEffects flip = SpriteEffects.None) {

			var tex = overrideTexture??Texture;
			var pData = parameterData;

			Draw.SetParameters(BaseEffect, (param, effect) => {
				switch (param.Name) {
					case "DiffuseColor":
						if (offsetColor != null) {
							param.SetValue(Color.ToVector4() * offsetColor.Value.ToVector4());
						}
						else {
							param.SetValue(Color.ToVector4());
						}
						return true;
					case "Texture":
						effect.SetParameter(param.Name, tex, flip);
						return true;
					case "World":
						param.SetValue(worldTransform);
						return true;
					default:
						if (pData.ContainsKey(param.Name)) {
							var data = pData[param.Name];
							if (data is MTexture)
								effect.SetParameter(param.Name, data as MTexture);
							else if (data is Color)
								param.SetValue(data.ToVector4());
							else if (data is Color[])
								param.SetValue(((Color[])data).Select((a) => { return a.ToVector4(); }).ToArray());
							else if (param.ParameterType == EffectParameterType.Single && param.ParameterClass == EffectParameterClass.Scalar)
								param.SetValue(Convert.ToSingle(pData[param.Name]));
							else if (param.ParameterType == EffectParameterType.Int32 && param.ParameterClass == EffectParameterClass.Scalar)
								param.SetValue(Convert.ToInt32(pData[param.Name]));
							else
								param.SetValue(pData[param.Name]);
							return true;
						}
						return false;
				}
			});
		}
	}
}
