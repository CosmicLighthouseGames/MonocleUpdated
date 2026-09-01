using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;

namespace Monocle {
	public interface IDrawCall {

		int RenderOrder { get; set; }
		float GetDepth(Matrix matrix);

		void Render(GraphicsDevice device);
	}
	public struct PriorityDrawCall : IDrawCall {
		public int RenderOrder { get; set; }
		public float GetDepth(Matrix matrix) => float.PositiveInfinity;

		public Action OnRender;

		public void Render(GraphicsDevice device) {
			OnRender?.Invoke();
		}
	}
	public unsafe static class Draw {

		public static float GetDepth(Matrix matrixA, Matrix matrixB) {
			matrixA = matrixA * matrixB;
			return matrixA.M43;
		}

		/// <summary>
		/// The currently-rendering Renderer
		/// </summary>
		public static Renderer Renderer { get; internal set; }

		public static GraphicsDevice GraphicsDevice { get; private set; }


		/// <summary>
		/// A subtexture used to draw rectangles and lines. 
		/// Will be generated at startup, but you can replace this with a subtexture from your Atlas to reduce texture swaps.
		/// Use the top left pixel of your Particle Subtexture if you replace it!
		/// Should be a 1x1 white pixel
		/// </summary>
		public static MTexture Pixel;
		public static MTexture Noise;

		static Matrix viewMatrix, worldProject;
		public static Matrix WorldProjection {
			get {
				return worldProject;
			}
			set {
				worldProject = value;
			}
		}
		public static Matrix ViewMatrix {
			get {
				return viewMatrix;
			}
			set {
				viewMatrix = value;
			}
		}

		public static DepthStencilState DefaultDepthState;
		public static DepthStencilState FallbackDepthState;

		public static bool InvertDepthBuffer = false;

		static Effect effect;
		public static Effect DefaultEffect {
			get => effect;
		}
		static Material defaultMaterial;
		public static Material DefaultMaterial => defaultMaterial;

		public static void SetDefaultEffect(string name) {
			effect = Material.GetEffect(name);
			defaultMaterial = Material.FromEffect(name);
		}
		public static void SetDefaultEffect(Material material) {
			effect = material.BaseEffect;
			defaultMaterial = material;
		}

		public static Material OverridingMaterial;

		public static Dictionary<int, DepthStencilState> StencilPasses = new Dictionary<int, DepthStencilState>();

		public static DepthStencilState stencilWrite, stencilRead;

		public static PixelFont DefaultFont;


		public const int FARTHEST_DEPTH = DepthPrecision >> 1;
		public const int CLOSEST_DEPTH = -(DepthPrecision >> 1);

		private const int DepthPrecision = 1 << 20;

		private const float SB_DEPTH_DIV = 1f / DepthPrecision;

		private static Rectangle rect;

		class DrawCallList {

			private List<IDrawCall> callLists;

			int renderQueue = 1;

			public DrawCallList() {
				callLists = new List<IDrawCall>();
			}

			public void Add(IDrawCall call) {

				callLists.Add(call);

			}

			public IEnumerable<IDrawCall> GetItems() {

				CurrentDrawCalls += callLists.Count;

				foreach (var item in callLists.OrderBy(a => { return a.GetDepth(viewMatrix); }).OrderBy(a=> { return (a is PriorityDrawCall ? 0 : 1); }).OrderBy(a => { return a.RenderOrder; })) {
					yield return item;
				}

				callLists.Clear();
			}

		}

		public struct SpriteDrawCall : IDrawCall {

			static MeshPointer mesh;

            public static void SetIndex()
            {
                mesh.SetIndex();
            }
            public static void RenderSprite()
            {
                mesh.RenderList();
			}

			public static void Initialize() {
				mesh = MeshHeap.CreateSection(new MonocleVertex[] {
					new MonocleVertex() {
						Position = new Vector3(0, 0, 0),
						TextureCoordinate = new Vector2(0, 1),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
					new MonocleVertex() {
						Position = new Vector3(1, 0, 0),
						TextureCoordinate = new Vector2(1, 1),
						Normal = Vector3.Backward,
						Binormal = Vector3.Up,
						Tangent = Vector3.Left,
						Color = Vector4.One,
					},
					new MonocleVertex() {
						Position = new Vector3(0, 1, 0),
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
					}
				},
				new short[]{
					0,
					1,
					2,
					1,
					3,
					2,
				});

			}
			static void AddDrawCalls(MTexture texture, Matrix transform, Color color, SpriteEffects flip, Material material) {

				transform = Matrix.CreateScale(texture.ClipRect.Width, texture.ClipRect.Height, 1) * transform;
				var mat = material??DefaultMaterial;

				foreach (var p in mat.Passes) {

					drawCall.Add(
						new SpriteDrawCall() {
							material = mat,
							overrideTexture = texture,
							flip = flip,
							color = color,
							worldTransform = transform,
							RenderOrder = p.Key
						}
					);
				}
			}



			public static void Draw(MTexture texture, Matrix transform, Material mat = null) {
				if (texture == null)
					return;
				AddDrawCalls(texture, transform, Color.White, SpriteEffects.None, mat);
			}
			public static void Draw(MTexture texture, Matrix transform, Color color, Material mat = null) {
				if (texture == null)
					return;
				AddDrawCalls(texture, transform, color, SpriteEffects.None, mat);
			}
			public static void Draw(MTexture texture, Matrix transform, Color color, SpriteEffects flip, Material mat = null) {
				if (texture == null)
					return;
				AddDrawCalls(texture, transform, color, flip, mat);
			}


			public int RenderOrder { get; set; }

			public float GetDepth(Matrix matrix) {
				return Monocle.Draw.GetDepth(matrix, worldTransform);
			}

			public Material material;
			public MTexture overrideTexture;
			public Matrix worldTransform = Matrix.Identity;
			public SpriteEffects flip;
			public Color color;

			public SpriteDrawCall() {
				material = null;
				overrideTexture = null;
				worldTransform = Matrix.Identity;
				flip = SpriteEffects.None;
				color = Color.White;
				RenderOrder = 0;
			}

			public void Render(GraphicsDevice device) {

				if (overrideTexture == null)
					return;

				var mat = OverridingMaterial??material;

				if (!mat.Passes.ContainsKey(RenderOrder))
					return;

				mat.SetParameters(worldTransform, overrideTexture, color, flip);

				mat.Render(RenderOrder, mesh.Render);
			}
		}



		private static DrawCallList drawCall;

		public static int PreviousDrawCalls;
		static int CurrentDrawCalls;

		private static DrawCallList[] drawStack = new DrawCallList[10];
		private static Matrix[] matrixStack = new Matrix[10];
		private static int stackIndex;

		public static event Func<Effect,EffectParameter, bool> OnParameterSet;

		public static void SetParameters(Effect effect, Func<Effect, EffectParameter, bool> changeParameter) {

			foreach (var param in effect.Parameters) {
				if (!changeParameter(effect, param)) {
					switch (param.Name) {
						case "Viewport": {
							var viewport = GraphicsDevice.Viewport;
							param.SetValue(new Vector4(viewport.X, viewport.Y, viewport.Width, viewport.Height));
						}
							break;
						case "CameraInverted":
							param.SetValue(Matrix.Invert(viewMatrix) * Matrix.Invert(worldProject));
							break;
						case "World2Screen":
							param.SetValue(worldProject);
							break;
						case "WorldViewProject":
							param.SetValue(viewMatrix);
							break;
						case "NoiseTexture":
							param.SetValue(Noise.Texture);
							break;
						case "CameraPosition":
							param.SetValue(Camera.Main.Position);
							break;
						default:
							try {
								if (OnParameterSet != null && OnParameterSet.Invoke(effect, param))
									continue;

								switch (param.ParameterType) {
									case EffectParameterType.Bool:
										param.SetValue(false);
										break;
									case EffectParameterType.Int32:
										param.SetValue(0);
										break;
									case EffectParameterType.Single:
										if (param.ParameterClass == EffectParameterClass.Matrix) {
											param.SetValue(Matrix.Identity);
										}
										else if (param.ParameterClass == EffectParameterClass.Vector) {
											switch (param.ColumnCount) {
												case 4:
													if (param.Elements.Count > 1) {
														param.SetValue(new Vector4[param.Elements.Count]);
													}
													else {
														param.SetValue(Vector4.Zero);
													}
													break;
												default:
													param.SetValue(0.0f);
													break;
											}
										}
										else {
											param.SetValue(0.0f);
										}
										break;
									case EffectParameterType.Texture2D:
										param.SetValue((Texture2D)null);
										break;
									case EffectParameterType.Texture3D:
										param.SetValue((Texture3D)null);
										break;
								}

							}
							catch {

							}
							break;
					}
					// set default
				}
			}
		}
		internal static void UpdatePerFrame() {

			PreviousDrawCalls = CurrentDrawCalls;
			CurrentDrawCalls = 0;
			

			// Just in case we need to update things before rendering
		}
		internal static void Initialize(GraphicsDevice graphicsDevice) {
			GraphicsDevice = graphicsDevice;
			Material.Initialize();

			MeshHeap.Initialize(GraphicsDevice);

			SpriteDrawCall.Initialize();
			
			

			DefaultDepthState = new DepthStencilState();
			DefaultDepthState.ReadFrom(DepthStencilState.Default);
			FallbackDepthState = new DepthStencilState();
			FallbackDepthState.ReadFrom(DepthStencilState.Default);


			UseDebugPixelTexture();

			for (int i = 0; i < drawStack.Length; i++) {
				drawStack[i] = new DrawCallList();
				matrixStack[i] = Matrix.Identity;
			}

			drawCall = drawStack[0];
			WorldProjection = Matrix.Identity;

			stencilWrite = new DepthStencilState();
			stencilRead = new DepthStencilState();


			stencilWrite.StencilDepthBufferFail = StencilOperation.Keep;
			stencilWrite.DepthBufferEnable = true;
			stencilWrite.DepthBufferFunction = CompareFunction.GreaterEqual;
			stencilWrite.CounterClockwiseStencilDepthBufferFail = StencilOperation.Keep;

			stencilWrite.StencilFunction = CompareFunction.Always;
			stencilWrite.StencilPass = StencilOperation.Replace;
			stencilWrite.StencilFail = StencilOperation.Replace;
			stencilWrite.StencilEnable = true;

			stencilRead.ReadFrom(stencilWrite);
			stencilRead.StencilFail = StencilOperation.Keep;
			stencilRead.StencilPass = StencilOperation.Keep;
			stencilRead.DepthBufferEnable = false;

			Draw.SetDefaultEffect("Monocle/default_material");
		}

		public static void UseDebugPixelTexture() {
			Color[] noise = new Color[64 * 64];
			for (int i = 0; i < noise.Length; i++) {
				noise[i] = new Color(Calc.Random.Range(0, 1.0f), Calc.Random.Range(0, 1.0f), Calc.Random.Range(0, 1.0f));
			}
			Pixel = new MTexture(1, 1, Color.White);
			Noise = new MTexture(64, 64, Color.White);
			Noise.Texture.SetData(noise);
		}

		public static void RenderPass() {

			var width = GraphicsDevice.Viewport.Width;

			foreach (var draw in drawCall.GetItems()) {
				draw.Render(GraphicsDevice);
				if (width != GraphicsDevice.Viewport.Width) {
					//GraphicsDevice.Viewport = new Viewport(GraphicsDevice.Viewport.X, GraphicsDevice.Viewport.Y, width, GraphicsDevice.Viewport.Height);
				}
			}
		}

		public static void ClearGraphics(float x, float y) {

			ClearGraphics(new Vector2(x, y));
		}
		public static void ClearGraphics(Vector2? size = null) {

			Vector2 winSize = size??new Vector2(Engine.WindowWidth, Engine.WindowHeight);

			WorldProjection = 
				Matrix.CreateScale(2.0f / winSize.X, 2.0f / winSize.Y, -0.01f) *
				Matrix.CreateTranslation(-1f, -1f, 0.5f);
		}
		public static void PushDrawStack() {

			matrixStack[stackIndex] = WorldProjection;

			stackIndex += 1;

			drawCall = drawStack[stackIndex];
			WorldProjection = Matrix.Identity;

		}
		public static void PopDrawStack() {
			if (stackIndex == 0)
				throw new Exception();

			stackIndex -= 1;

			WorldProjection = matrixStack[stackIndex];

			drawCall = drawStack[stackIndex / 2];
		}


		public static void CustomDrawCall(IDrawCall call) {
			drawCall.Add(call);
		}

		#region 3D Images

		public static void Texture(MTexture tex, Matrix matrix, Color color, SpriteEffects flipping = SpriteEffects.None, Material mat = null) {

			if (tex == null)
				return;

			if (mat == null) {
				mat = DefaultMaterial;
			}

			SpriteDrawCall.Draw(tex, matrix, color, flipping, mat);

		}
		public static void Texture(MTexture tex, Vector3 position, Vector2 origin, Color color, Vector2 scale, Material mat = null) {

			if (tex == null)
				return;

			var matrix = Matrix.Identity
				* Matrix.CreateScale(1f / Engine.PixelsPerUnit, 1f / Engine.PixelsPerUnit, 1)
				* Matrix.CreateTranslation(-origin.X, -origin.Y, 0)
				* Matrix.CreateScale(scale.X, scale.Y, 1)
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, color, mat:mat);

		}
		public static void Texture(MTexture tex, Vector3 position, Material mat = null) {
			if (tex == null)
				return;

			var matrix = Matrix.Identity
				* Matrix.CreateScale(1f / Engine.PixelsPerUnit, 1f / Engine.PixelsPerUnit, 1)
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, Color.White);
		}

		public static void Texture(MTexture tex, Vector3 position, Vector2 origin, Vector2 scale, Quaternion rotation, Color color, Material mat = null, SpriteEffects flipping = SpriteEffects.None) {

			if (tex == null)
				return;

			var matrix = Matrix.Identity
				* Matrix.CreateTranslation(new Vector3(-origin.X, -origin.Y, 0))
				* Matrix.CreateScale(scale.X / Engine.PixelsPerUnit, scale.Y / Engine.PixelsPerUnit, 1)
				* Matrix.CreateFromQuaternion(rotation)
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, color, flipping, mat);
		}

		#endregion


		#region Rectangle

		public static void Rect(float x, float y, float width, float height, Color color) {
			Matrix mat = Matrix.Identity
				* Matrix.CreateScale(width, height, 1)
				* Matrix.CreateTranslation(x, y, 0)
				;
			Texture(Pixel, mat, color);
		}
		public static void Rect(float x, float y, float z, float width, float height, Color color) {
			Matrix mat = Matrix.Identity
				* Matrix.CreateScale(width, height, 1)
				* Matrix.CreateTranslation(x, y, z)
				;
			Texture(Pixel, mat, color);
		}
		public static void Rect(Vector2 pos, Vector2 size, Color color) {
			Rect(pos.X, pos.Y, size.X, size.Y, color);
		}
		public static void Rect(Vector3 pos, Vector2 size, Color color) {
			Rect(pos.X, pos.Y, pos.Z, size.X, size.Y, color);
		}
		public static void Rect(Rectangle rect, Color color) {
			Rect(rect.X, rect.Y, rect.Width, rect.Height, color);
		}

		#endregion
	}
}
