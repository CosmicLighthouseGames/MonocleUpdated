using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Monocle {
	public interface IDrawCall {

		void Render(GraphicsDevice device);
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

		static Effect effect;
		public static Effect DefaultEffect {
			get => effect;
		}
		static Material material;
		public static Material DefaultMaterial => material;

		public static void SetDefaultEffect(string name) {
			effect = Material.GetEffect(name);
			material = new Material(name);
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

		private static Rectangle rect;

		class DrawCallList {

			private List<IDrawCall> callList;

			bool dirty = false;

			private int count;

			public int Count => count;

			public DrawCallList() {
				callList = new List<IDrawCall>();
			}

			/// <summary>
			/// Add index value from the beginning.  If the last index is the same, return
			/// Set X to the smallest value
			/// Do a binary search for the last value of X and increment index up 1 to find the start of the next value clump
			/// </summary>
			private void Settle() {


				dirty = false;
			}

			public void Add(IDrawCall call) {

				dirty = true;

				callList.Add(call);

				count++;
			}
			public void Clear() {
				callList.Clear();
				dirty = false;

				count = 0;
			}

			public IEnumerable<IDrawCall> GetItems() {

				foreach (var item in callList) {
					yield return item;
				}

				yield break;

			}

			public void Sort() {
				if (dirty) {
					Settle();
				}

			}
		}
		public class SpriteDrawCall : IDrawCall {
			const int BUFFER_SIZE = 4 * 0x2000;
			static List<MonocleVertex[]> meshes;
			static MonocleVertex* meshPointer;
			static int listIndex = 0, arrayIndex = 0;
			public static int[] indices = new int[]{
				1, 2, 3, 2, 1, 0
			};

			public static void Initialize() {
				meshes = new List<MonocleVertex[]>();
				meshes.Add(new MonocleVertex[BUFFER_SIZE]);
				ClearGraphics();
			}
			public static void ClearGraphics() {
				fixed (MonocleVertex* ptr = &meshes[0][0]) {
					meshPointer = ptr;
				}
				listIndex = 0;
				arrayIndex = 0;
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
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Color color, SpriteEffects flip, int stencil = 0, Material mat = null) {
				var retval = AddMesh(transform, color, texture.Texture, texture.ClipRect, flip);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;
				retval.stencil = stencil;

				return retval;
			}
			public static SpriteDrawCall Draw(MTexture texture, Matrix transform, Color color, Rectangle clipRect, SpriteEffects flip, int stencil = 0, Material mat = null) {
				var retval = AddMesh(transform, color, texture.Texture, clipRect, flip);

				retval.material = mat??DefaultMaterial;
				retval.overrideTexture = texture;
				retval.stencil = stencil;

				return retval;
			}

			static SpriteDrawCall AddMesh(Matrix transform, Color color, Vector2 size, Vector2 uvStart, Vector2 uvSize, SpriteEffects flip) {

				var mesh = meshes[listIndex];
				var index = arrayIndex;

				Vector3 normal = Vector3.TransformNormal(Vector3.Backward, transform),
					binormal = Vector3.TransformNormal(Vector3.Left, transform),
					tangent = Vector3.Transform(Vector3.Up, transform);

				for (int i = 0; i < 4; i++) {
					float x = (i & 1);
					float y = (i >> 1);

					bool uX = (x > 0) != ((flip & SpriteEffects.FlipHorizontally) != SpriteEffects.None);
					bool uY = (y > 0) == ((flip & SpriteEffects.FlipVertically) != SpriteEffects.None);

					x *= size.X;
					y *= size.Y;
					*meshPointer = new MonocleVertex() {
						Position = Vector3.Transform(new Vector3(x, y, 0), transform),
						TextureCoordinate = new Vector2(uvStart.X + (uX ? uvSize.X : 0), uvStart.Y + (uY ? uvSize.Y : 0)),
						Color = color.ToVector4(),
						Normal = normal,
						Binormal = binormal,
						Tangent = tangent,
					};
					meshPointer++;
				}
				arrayIndex += 4;

				if (arrayIndex >= BUFFER_SIZE) {
					listIndex++;
					if (meshes.Count <= listIndex) {
						meshes.Add(new MonocleVertex[BUFFER_SIZE]);
					}
					fixed (MonocleVertex* ptr = &meshes[listIndex][0]) {
						meshPointer = ptr;
					}
				}
				
				return new SpriteDrawCall() {
					mesh = mesh,
					start = index
				};
			}
			static SpriteDrawCall AddMesh(Matrix transform, Color color, Texture2D texture, Rectangle clipRect, SpriteEffects flip) {

				return AddMesh(transform, color, new Vector2(clipRect.Width, clipRect.Height),
					new Vector2((float)clipRect.X / texture.Width, (float)clipRect.Y / texture.Height),
					new Vector2((float)clipRect.Width / texture.Width, (float)clipRect.Height / texture.Height), flip);

			}


			public Material material;
			public MTexture overrideTexture;
			public MonocleVertex[] mesh;
			public int start;
			public int? stencil;

			public void Render(GraphicsDevice device) {

				var mat = OverridingMaterial??material;

				var tech = mat.Technique;
				var techPass = tech.Passes[0];

				var stencil = this.stencil??mat.Stencil;

				if (stencil != device.DepthStencilState.ReferenceStencil) {
					var dsMask = new DepthStencilState();
					dsMask.ReadFrom(device.DepthStencilState);
					dsMask.ReferenceStencil = stencil;
					device.DepthStencilState = dsMask;
				}

				var tex = overrideTexture??mat.Texture;
				var drawcall = this;
				var pData = mat.parameterData;

				SetParameters(mat.BaseEffect, (param) => {
					switch (param.Name) {
						case "DiffuseColor":
							param.SetValue(mat.Color.ToVector4());
							return true;
						case "Texture":
							param.SetValue(tex.Texture);
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
				device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, mesh, start, 4, indices, 0, 2);

			}
		}



		private static DrawCallList opaque;

		private static DrawCallList[] drawStack = new DrawCallList[10];
		private static Matrix[] matrixStack = new Matrix[10];
		private static int stackIndex;

		public static event Func<EffectParameter, bool> OnParameterSet;

		public static void SetParameters(Effect effect, Func<EffectParameter, bool> changeParameter) {

			foreach (var param in effect.Parameters) {
				if (!changeParameter(param)) {
					switch (param.Name) {
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
							if (OnParameterSet?.Invoke(param)??false)
								continue;

							switch (param.ParameterType) {
								case EffectParameterType.Bool:
									param.SetValue(false);
									break;
								case EffectParameterType.Int32:
									if (param.ParameterClass == EffectParameterClass.Scalar) {

									}
									param.SetValue(0);
									break;
								case EffectParameterType.Single:
									if (param.ParameterClass == EffectParameterClass.Scalar) {

									}
									if (param.ParameterClass == EffectParameterClass.Matrix) {
										param.SetValue(Matrix.Identity);
									}
									else if (param.ParameterClass == EffectParameterClass.Vector) {
										switch (param.ColumnCount) {
											case 4:
												param.SetValue(Vector4.Zero);
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
							break;
					}
					// set default
				}
			}
		}
		internal static void UpdatePerFrame() {

			Depth = 0;

			// Just in case we need to update things before rendering
		}
		internal static void Initialize(GraphicsDevice graphicsDevice) {
			GraphicsDevice = graphicsDevice;
			Material.Initialize();
			SpriteDrawCall.Initialize();

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
			FilterCall.Initialize();
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

			opaque.Sort();

			foreach (var draw in opaque.GetItems()) {
				draw.Render(GraphicsDevice);
			}
		}

		public static void ClearGraphics(float x, float y) {

			ClearGraphics(new Vector2(x, y));
		}
		public static void ClearGraphics(Vector2? size = null) {

			SpriteDrawCall.ClearGraphics();

			Vector2 winSize = size??new Vector2(Engine.WindowWidth, Engine.WindowHeight);

			opaque.Clear();
			WorldProjection = 
				Matrix.CreateScale(2.0f / winSize.X, 2.0f / winSize.Y, -0.01f) *
				Matrix.CreateTranslation(-1f, -1f, 0.5f);
		}
		public static void PushDrawStack() {

			matrixStack[stackIndex] = WorldProjection;

			stackIndex += 1;

			opaque = drawStack[stackIndex];
			WorldProjection = Matrix.Identity;

			opaque.Clear();
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

		public static void Texture(MTexture tex, Matrix matrix, Color color, int stencil = 0, SpriteEffects flipping = SpriteEffects.None, Material mat = null) {

			if (mat == null) {
				mat = DefaultMaterial;
			}

			opaque.Add(SpriteDrawCall.Draw(tex, matrix, color, flipping, stencil, mat));

		}
		public static void Texture(MTexture tex, Vector3 position, Material mat = null) {

			var matrix = Matrix.Identity
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, Color.White);
		}

		public static void Texture(MTexture tex, Vector3 position, Vector2 origin, Vector2 scale, Quaternion rotation, Color color, int stencil = 0, SpriteEffects flipping = SpriteEffects.None) {

			var matrix = Matrix.Identity
				* Matrix.CreateTranslation(new Vector3(-origin.X, -origin.Y, 0))
				* Matrix.CreateScale(scale.X / Engine.PixelsPerUnit, scale.Y / Engine.PixelsPerUnit, 1)
				* Matrix.CreateFromQuaternion(rotation)
				* Matrix.CreateTranslation(position.X, position.Y, position.Z)
				;

			Texture(tex, matrix, color, stencil, flipping);
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
		public static void Rect(Vector2 pos, Vector2 size, Color color) {
			Rect(pos.X, pos.Y, size.X, size.Y, color);
		}
		public static void Rect(Rectangle rect, Color color) {
			Rect(rect.X, rect.Y, rect.Width, rect.Height, color);
		}

		#endregion
	}
}
