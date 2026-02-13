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

		void Render(GraphicsDevice device);
	}
	public struct PriorityDrawCall : IDrawCall {
		public int RenderOrder { get; set; }

		public Action OnRender;

		public void Render(GraphicsDevice device) {
			OnRender?.Invoke();
		}
	}
	public unsafe static class Draw {

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

		static Matrix worldProj, worldProjInvert;
		public static Matrix WorldProjection {
			get {
				return worldProj;
			}
			set {
				worldProj = value;
				worldProjInvert = Matrix.Invert(worldProj);
			}
		}
		public static Matrix WorldProjectionInverted {
			get {
				return worldProjInvert;
			}
		}

		public static DepthStencilState DefaultDepthState;
		public static DepthStencilState FallbackDepthState;

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

		public static DepthStencilState stencilWrite, stencilRead;

		public static PixelFont DefaultFont;


		public const int FARTHEST_DEPTH = DepthPrecision >> 1;
		public const int CLOSEST_DEPTH = -(DepthPrecision >> 1);

		private const int DepthPrecision = 1 << 20;

		private const float SB_DEPTH_DIV = 1f / DepthPrecision;

		public static int Depth {
			get { return entityDepth; }
			set {
				entityDepth = value;
				RealDepth = (value * SB_DEPTH_DIV) + 0.5f;
			}
		}
		public static float RealDepth;
		private static int entityDepth;

		public static int CurrentRenderOrder;

		private static Rectangle rect;

		class DrawCallList {

			private PriorityQueue<IDrawCall, (int, int)> callLists;

			int renderQueue = 1;

			public DrawCallList() {
				var compare = Comparer<(int, int)>.Create(
					((int pass, int order) a, (int pass, int order) b) => {
					if (a.pass == b.pass)
						return a.order.CompareTo(b.order);
					return a.pass.CompareTo(b.pass);
				});
				callLists = new PriorityQueue<IDrawCall, (int, int)>(compare);
			}

			public void Add(IDrawCall call) {

				callLists.Enqueue(call, (call.RenderOrder, (call is PriorityDrawCall ? 0 : renderQueue++)));

			}

			public IEnumerable<IDrawCall> GetItems() {

				CurrentDrawCalls += callLists.Count;

				renderQueue = 1;

				while (callLists.Count > 0) {
					var queued = callLists.Dequeue();


					yield return queued;
				}
				yield break;

			}

		}
		public struct SpriteDrawCall : IDrawCall {

			static MeshPointer mesh;

			public static void SetBuffers() {
				mesh.SetIndex();
			}
			public static void RenderSprite() {
				mesh.RenderTriangleList();
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
					2,
					1,
					1,
					2,
					3,
				});

			}
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Material mat = null) {
				var retval = AddMesh(transform, Color.White, texture.Texture, texture.ClipRect, SpriteEffects.None);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;

				return retval;
			}
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Color color, Material mat = null) {
				var retval = AddMesh(transform, color, texture.Texture, texture.ClipRect, SpriteEffects.None);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;

				return retval;
			}
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Color color, SpriteEffects flip, DepthStencilState? stencil = null, Material mat = null) {
				if (texture == null)
					return default;
				var retval = AddMesh(transform, color, texture.Texture, texture.ClipRect, flip);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;
				retval.DepthStencilState = stencil;

				return retval;
			}
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Color color, Rectangle clipRect, SpriteEffects flip, DepthStencilState? stencil = null, Material mat = null) {
				var retval = AddMesh(transform, color, texture.Texture, clipRect, flip);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;
				retval.DepthStencilState = stencil;

				return retval;
			}

			static SpriteDrawCall AddMesh(Matrix transform, Color color, Vector2 size, SpriteEffects flip) {

				transform = Matrix.CreateScale(size.X, size.Y, 1) * transform;
				return new SpriteDrawCall() {
					flip = flip,
					color = color,
					worldTransform = transform,
					RenderOrder = CurrentRenderOrder
				};
			}
			static SpriteDrawCall AddMesh(Matrix transform, Color color, Texture2D texture, Rectangle clipRect, SpriteEffects flip) {

				return AddMesh(transform, color, new Vector2(clipRect.Width, clipRect.Height), flip);

			}

			public int RenderOrder { get; set; }

			public Material material;
			public MTexture overrideTexture;
			public Matrix worldTransform = Matrix.Identity;
			public SpriteEffects flip;
			public Color color;
			public DepthStencilState DepthStencilState;

			public SpriteDrawCall() {
				this.material = null;
				this.overrideTexture = null;
				worldTransform = Matrix.Identity;
				flip = SpriteEffects.None;
				color = Color.White;
				DepthStencilState = null;
				RenderOrder = 0;
			}

			public void Render(GraphicsDevice device) {

				if (overrideTexture == null)
					return;

				var mat = OverridingMaterial??material;

				var tech = mat.GetTechnique(RenderOrder);
				var techPass = tech.Passes[0];

				var stencil = DepthStencilState??mat.DepthStencilState??FallbackDepthState;
				device.DepthStencilState = stencil;

				mat.SetParameters(worldTransform, overrideTexture, color, flip);
				

				techPass.Apply();

				mesh.SetIndex();
				mesh.RenderTriangleList();


			}
		}



		private static DrawCallList opaque;

		public static int PreviousDrawCalls;
		static int CurrentDrawCalls;

		private static DrawCallList[] drawStack = new DrawCallList[10];
		private static Matrix[] matrixStack = new Matrix[10];
		private static int stackIndex;

		public static event Func<EffectParameter, bool> OnParameterSet;

		public static void SetParameters(Effect effect, Func<EffectParameter, Effect, bool> changeParameter) {

			foreach (var param in effect.Parameters) {
				if (!changeParameter(param, effect)) {
					switch (param.Name) {
						case "Viewport": {
							var viewport = GraphicsDevice.Viewport;
							param.SetValue(new Vector4(viewport.X, viewport.Y, viewport.Width, viewport.Height));
						}
							break;
						case "WorldViewProj":
							param.SetValue(worldProj);
							break;
						case "WorldViewProjInvert":
							param.SetValue(worldProjInvert);
							break;
						case "NoiseTexture":
							param.SetValue(Noise.Texture);
							break;
						case "ViewDirection":
							param.SetValue(Vector3.Transform(Vector3.Forward, Camera.Main.Rotation));
							break;
						default:
							try {
								if (OnParameterSet != null && OnParameterSet.Invoke(param))
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

			Depth = 0;
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

			opaque = drawStack[0];
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

			foreach (var draw in opaque.GetItems()) {
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

			opaque = drawStack[stackIndex];
			WorldProjection = Matrix.Identity;

		}
		public static void PopDrawStack() {
			if (stackIndex == 0)
				throw new Exception();

			stackIndex -= 1;

			WorldProjection = matrixStack[stackIndex];

			opaque = drawStack[stackIndex / 2];
		}


		public static void CustomDrawCall(IDrawCall call) {
			opaque.Add(call);
		}

		#region 3D Meshes

		public static void Mesh(VertexBuffer verts, IndexBuffer indices, Matrix matrix, Color color, Material material = null) {

			//opaque.Add(new DrawCall() {

			//	transform = matrix,
			//	material = material??Material.Shader(Pixel, color),
			//	stencil = (material == null) ? 0 : material.Stencil,
			//});
		}
		public static void Mesh(MonocleModel model, Matrix matrix, Color color, Material material = null) {

		}


		#endregion

		#region 3D Images

		public static void Texture(MTexture tex, Matrix matrix, Color color, DepthStencilState? stencil = null, SpriteEffects flipping = SpriteEffects.None, Material mat = null) {

			if (tex == null)
				return;

			if (mat == null) {
				mat = DefaultMaterial;
			}

			opaque.Add(SpriteDrawCall.Draw(tex, matrix, color, flipping, stencil, mat));

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

		public static void Texture(MTexture tex, Vector3 position, Vector2 origin, Vector2 scale, Quaternion rotation, Color color, Material mat = null, DepthStencilState? stencil = null, SpriteEffects flipping = SpriteEffects.None) {

			if (tex == null)
				return;

			var matrix = Matrix.Identity
				* Matrix.CreateTranslation(new Vector3(-origin.X, -origin.Y, 0))
				* Matrix.CreateScale(scale.X / Engine.PixelsPerUnit, scale.Y / Engine.PixelsPerUnit, 1)
				* Matrix.CreateFromQuaternion(rotation)
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, color, stencil, flipping, mat);
		}

		#endregion

		#region Screen Rectangle

		//public static void ScreenRect(float x, float y, float width, float height, Color color, Camera cam = null) {
		//	if (cam == null)
		//		cam = Camera.Main;

		//	Matrix mat = Matrix.Identity
		//		* cam.MatrixSprites
		//		* Matrix.CreateScale(width, height, 1)
		//		* Matrix.CreateTranslation(x, y, (RealDepth - 0.5f) * 250)
		//		;
		//	Texture3D(Pixel, mat, color);
		//}
		//public static void ScreenRect(Vector2 pos, Vector2 size, Color color, Camera cam = null) {
		//	ScreenRect(pos.X, pos.Y, size.X, size.Y, color, cam);
		//}

		#endregion

		#region Rectangle

		public static void Rect(float x, float y, float width, float height, Color color) {
			Matrix mat = Matrix.Identity
				* Matrix.CreateScale(width, height, 1)
				* Matrix.CreateTranslation(x, y, RealDepth)
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
