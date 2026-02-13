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

		static Dictionary<string, (Effect effect, EffectTechnique technique)> LoadedTechniques = new Dictionary<string, (Effect, EffectTechnique)>();


		static void AddEffect(Effect effect, string localPath) {

			bool addedDefault = false;
			foreach (var tech in effect.Techniques) {
				if (!addedDefault) {
					LoadedTechniques.Add($"{localPath}", (effect, tech));
					addedDefault = true;
				}
				LoadedTechniques.Add($"{localPath}.{tech.Name}", (effect, tech));
			}
		}

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
									var effect = new Effect(gd, File.ReadAllBytes("tmp/compiled.cso"));
									AddEffect(effect, localPath);
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

					if (!LoadedTechniques.ContainsKey(localPath)) {

						try {
							var effect = new Effect(gd, content.GetBinary());
							AddEffect(effect, localPath);
						}
						catch {
							ErrorLog.Write($"Error loading effect: {content.Path}");
						}
					}
				}

			}


			cmd.Close();
		}

		public static IEnumerable<EffectTechnique> LoadedEffects() {
			foreach (var technique in LoadedTechniques) {
				yield return technique.Value.technique;
			}
		}
		public static Effect GetEffect(string name) {
			if (LoadedTechniques.ContainsKey(name)) { return LoadedTechniques[name].effect; }
			return null;
		}
		public static EffectTechnique GetTechnique(string name) {
			if (LoadedTechniques.ContainsKey(name)) { return LoadedTechniques[name].technique; }
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

		public static Material FromEffect(string effect) {
			return new Material(effect);
		}
		public static Material FromEffects(string effect, params (int, string)[] data) {
			return new Material().SetTechniques(effect, data);
		}

		Material() {
			SetTechnique("Default");
			Color = Color.White;
			Name = "Default Material";
		}
		Material(string name) {
			if (!LoadedTechniques.ContainsKey(name))
				throw new Exception($"Missing {name} Material");
			SetTechnique(name);
			Color = Color.White;
			Name = name;
		}
		public Material(Material other) {
			foreach (var item in other.techniques) {
				techniques.Add((item.Item1, item.Item2));
			}
			BaseEffect = other.BaseEffect;
			//TechniqueID = other.TechniqueID;
			Color = other.Color;
			Name = other.Name;
			Texture = other.Texture;
			DepthStencilState = other.DepthStencilState;
			RenderOrder = other.RenderOrder;
			foreach (var param in other.parameterData) {
				parameterData[param.Key] = param.Value;
			}
		}

		public string Name { get; private set; }

		public Effect BaseEffect { get; private set; }

		List<(int, EffectTechnique)> techniques = new List<(int, EffectTechnique)>();

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

		public EffectTechnique GetTechnique(int pass) {

			for (int i = techniques.Count - 1; i >= 0; i--) {
				if (techniques[i].Item1 <= pass)
					return techniques[i].Item2;
			}

			return techniques[0].Item2;
		}

		public bool HasTechnique(string technique) {

			foreach (var item in techniques) {
				if (item.Item2.Name == technique) return true;
			}
			return false;
		}
		public Material SetTechnique(string technique) {

			if (technique == "Default") {
				techniques.Clear();

				BaseEffect = Draw.DefaultEffect;
				techniques.Add((0, BaseEffect.CurrentTechnique));
			}
			else if (LoadedTechniques.ContainsKey(technique)) {
				techniques.Clear();

				var lt = LoadedTechniques[technique];
				BaseEffect = lt.effect;
				techniques.Add((0, lt.technique));
			}
			else if (BaseEffect != null && BaseEffect.Techniques[technique] != null) {
				techniques.Clear();

				techniques.Add((0, BaseEffect.Techniques[technique]));
			}
			return this;
		}
		public Material SetTechniques(string effect, params (int, string)[] techs) {

			if (LoadedTechniques.ContainsKey(effect)) {
				techniques.Clear();

				var lt = LoadedTechniques[effect];
				BaseEffect = lt.effect;

				for (int i = 0; i < techs.Length; i++) {
					string key = $"{effect}.{techs[i].Item2}";

					if (LoadedTechniques.ContainsKey(key)) {
						techniques.Add((techs[i].Item1, LoadedTechniques[key].technique));
					}
					else {
						techniques.Add((techs[i].Item1, BaseEffect.CurrentTechnique));
					}
				}

				techniques.Sort((a, b) => a.Item1.CompareTo(b.Item1));
			}
			

			return this;
		}

		public Material		SetParameter(string name, object value) {
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
							param.SetValue(offsetColor.Value.ToVector4());
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
						if (pData.ContainsKey(param.Name) && pData[param.Name] != null) {
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
