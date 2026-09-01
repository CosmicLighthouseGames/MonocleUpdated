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
		public class Pass {
			internal EffectPass EP;

			public CullMode CullType = CullMode.CullClockwiseFace;
			public DepthStencilState DepthStencil {
				get => _dss?? (Draw.StencilPasses.GetValueOrDefault(_renderOrder, Draw.FallbackDepthState));
				set {
					if (_dss != value) {
						_dss = value;
					}
				}
			}
			public BlendState Blend {
				get => _bs;
				set {
					if (_bs != value) {
						_bs = value;
					}
				}
			}
			DepthStencilState _dss;
			BlendState _bs;
			int _renderOrder;

			public Pass(EffectPass pass, int renderOrder) {
				_renderOrder = renderOrder;
				EP = pass;
			}

			int _stencil;
			public int Stencil {
				get => _stencil;
				set {
					_stencil = value;
					SetStencil(value);
				}
			}

			public void EnableStencilRead(CompareFunction compare) {

				if (_dss == null) {
					_dss = new DepthStencilState();
					if (Draw.StencilPasses.ContainsKey(_renderOrder)) {
						_dss.ReadFrom(Draw.StencilPasses[_renderOrder]);
					}
					else {
						_dss.ReadFrom(Draw.FallbackDepthState);
					}
				}

				_dss.StencilEnable = true;
				_dss.StencilFunction = compare;
				_dss.CounterClockwiseStencilFunction = CompareFunction.Always;
				_dss.StencilPass = StencilOperation.Keep;

				SetStencil(_stencil);
			}
			public void EnableStencilWrite() {

				if (_dss == null) {
					_dss = new DepthStencilState();
					if (Draw.StencilPasses.ContainsKey(_renderOrder)) {
						_dss.ReadFrom(Draw.StencilPasses[_renderOrder]);
					}
					else {
						_dss.ReadFrom(Draw.FallbackDepthState);
					}
				}

				_dss.StencilEnable = true;
				_dss.StencilFunction = CompareFunction.Always;
				_dss.CounterClockwiseStencilFunction = CompareFunction.Always;
				_dss.StencilPass = StencilOperation.Replace;

				SetStencil(_stencil);
			}

			public void SetStencil(int stencil) {

				_stencil = stencil;

				// In Read Mode
				if (_dss.StencilPass == StencilOperation.Keep) {
					_dss.StencilWriteMask = 0;
				}
				else {
					_dss.StencilWriteMask = int.MaxValue;
				}
				_dss.StencilMask = int.MaxValue;
				_dss.ReferenceStencil = stencil;
			}

		}

		public static Dictionary<string, int> PassOrders = new Dictionary<string, int>();

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
			//Task.Factory.StartNew(() => {

			//	while (!se.EndOfStream) {
			//		var line = se.ReadLine();

			//		if (Regex.IsMatch(line, @"effect\.fx\([\d-,]+\): error")) {
			//			error = true;
			//		}
			//	}
			//});
			//Task.Factory.StartNew(() => {

			//	while (!so.EndOfStream) {
			//		var line = so.ReadLine();
			//	}
			//});

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

		Material() {
			SetTechnique("Default");
			Color = Color.White;
			Name = "Default Material";
		}
		Material(string name) {
			if (!LoadedTechniques.ContainsKey(name)){
				//throw new Exception($"Missing {name} Material");

				SetTechnique("Default");
				Color = Color.White;
                Name = "Default Material";

                return;
            }
			SetTechnique(name);
			Color = Color.White;
			Name = name;
		}
		public Material(Material other) {
			Technique = other.Technique;
			Passes = other.Passes;

			BaseEffect = other.BaseEffect;
			//TechniqueID = other.TechniqueID;
			Color = other.Color;
			Name = other.Name;
			Texture = other.Texture;
			foreach (var param in other.parameterData) {
				parameterData[param.Key] = param.Value;
			}
		}

		public string Name { get; private set; }


        public Effect BaseEffect { get; private set; }

		public EffectTechnique Technique { get; private set; }

		public Dictionary<int, Pass[]> Passes { get; private set; }

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


		public Dictionary<string, object> parameterData = new Dictionary<string, object>();


		public Material SetTechnique(string technique) {
			
			if (technique == "Default") {
				BaseEffect = Draw.DefaultEffect;
				Technique = BaseEffect.CurrentTechnique;
			}
			else if (BaseEffect != null && BaseEffect.Techniques[technique] != null) {

				Technique = BaseEffect.Techniques[technique];
			}
			else if (LoadedTechniques.ContainsKey(technique)) {

				var lt = LoadedTechniques[technique];
				BaseEffect = lt.effect;
				Technique = lt.technique;
			}

			var temp = new Dictionary<int, List<EffectPass>>();

			foreach (var pass in Technique.Passes) {
				int index = 0;
				if (PassOrders.ContainsKey(pass.Name)) {
					index = PassOrders[pass.Name];
				}
				if (!temp.ContainsKey(index)) {
					temp[index] = new List<EffectPass>();
				}
				temp[index].Add(pass);
			}

			Passes = temp.ToDictionary(x => x.Key, x => x.Value.Select(a => { return new Pass(a, x.Key); }).ToArray() );

			return this;
		}

		public Material	SetParameter(string name, object value) {
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

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value){ 
					pass.SetStencil(stencil);
				}
			}
			return this;

		}
		public Material SetCulling(CullMode cull) {

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value) {
					pass.CullType = cull;
				}
			}
			return this;
		}

		public Material SetCustomDepthStencilState(DepthStencilState custom) {

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value) {
					pass.DepthStencil = custom;
				}
			}
			return this;
		}
		public Material SetCustomBlendState(BlendState custom) {

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value) {
					pass.Blend = custom;
				}
			}
			return this;
		}
		public Material EnableStencilWrite() {

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value) {
					pass.EnableStencilWrite();
				}
			}
			return this;
		}
		public Material EnableStencilRead(CompareFunction function) {

			foreach (var passes in Passes) {
				foreach (var pass in passes.Value) {
					pass.EnableStencilRead(function);
				}
			}
			return this;
		}

		bool invertedScale;
		public void Render(int order, Action OnApply) {

			foreach (var pass in Passes[order]) {

				Draw.GraphicsDevice.DepthStencilState = pass.DepthStencil;
				BlendState oldState = null;
				if (pass.Blend != null) {
					oldState = Draw.GraphicsDevice.BlendState;
					Draw.GraphicsDevice.BlendState = pass.Blend;
				}

				if (pass.CullType == CullMode.None) {
					Draw.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
				}
				else {
					Draw.GraphicsDevice.RasterizerState = (invertedScale == (pass.CullType == CullMode.CullClockwiseFace)) ? RasterizerState.CullClockwise : RasterizerState.CullCounterClockwise;
				}


				pass.EP.Apply();
				OnApply();

				if (pass.Blend != null) {
					Draw.GraphicsDevice.BlendState = oldState;
				}
			}
		}

		public void CopyParameters(Material other) {
			parameterData.Clear();
			foreach (var param in other.parameterData) {
				parameterData[param.Key] = param.Value;
			}
		}
		public Material Clone() {
			var newMat = new Material();
			newMat.BaseEffect = BaseEffect;
			newMat.SetTechnique(Technique.Name);
			newMat.CopyParameters(this);

			return newMat;
		}

		public void SetParameters(Matrix worldTransform, MTexture overrideTexture, Color? offsetColor = null, SpriteEffects flip = SpriteEffects.None) {


			invertedScale = Vector3.Dot(Vector3.Cross(worldTransform.Up, worldTransform.Left), worldTransform.Forward) <= 0;

			var tex = overrideTexture??Texture;
			var pData = parameterData;

			Draw.SetParameters(BaseEffect, (effect, param) => {
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
								param.SetValue(((Color)data).ToVector4());
							else if (data is Color[])
								param.SetValue(((Color[])data).Select((a) => { return a.ToVector4(); }).ToArray());
							else if (param.ParameterType == EffectParameterType.Single && param.ParameterClass == EffectParameterClass.Scalar)
								param.SetValue(Convert.ToSingle(pData[param.Name]));
							else if (param.ParameterType == EffectParameterType.Int32 && param.ParameterClass == EffectParameterClass.Scalar)
								param.SetValue(Convert.ToInt32(pData[param.Name]));
							else
								param.SetValue((dynamic)pData[param.Name]);
							return true;
						}
						return false;
				}
			});


		}
	}
}
