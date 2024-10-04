//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Graphics;

//namespace Monocle {

//	public abstract class ScreenFilter {

//		static VertexPositionTexture[] screenQuad;
//		static VertexBuffer vBuffer;
//		static IndexBuffer iBuffer;
//		public static DepthStencilState DepthState;
//		static string[] PassNames = new string[]{
//			"Fantastic",
//			"Great",
//			"Good",
//			"Low",
//			"Potato",
//		};


//		static ScreenFilter() {
//		}

//		public static void LoadContent() {

//			screenQuad = new VertexPositionTexture[4];


//			screenQuad[0].Position = new Vector3(-1, 1, 0);
//			screenQuad[1].Position = new Vector3(1, 1, 0);
//			screenQuad[2].Position = new Vector3(-1, -1, 0);
//			screenQuad[3].Position = new Vector3(1, -1, 0);

//			DepthState = new DepthStencilState();
//			DepthState.DepthBufferEnable = false;
//			DepthState.DepthBufferWriteEnable = false;

//			vBuffer = new VertexBuffer(Draw.GraphicsDevice, typeof(VertexPositionTexture), 4, BufferUsage.WriteOnly);
//			vBuffer.SetData(screenQuad);
//			iBuffer = new IndexBuffer(Draw.GraphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.None);
//			iBuffer.SetData(new short[] {
//				0, 1, 2, 1, 3, 2
//			});
//		}

//		static Rectangle viewRect, screenSize;



//		public static void SetFilterSize(Rectangle rect) {

//			viewRect = rect;

//			//screenQuad[0].TextureCoordinate = new Vector2((float)rect.Left / RenderTargets.TEX_WIDTH, (float)rect.Top / RenderTargets.TEX_HEIGHT);
//			//screenQuad[1].TextureCoordinate = new Vector2((float)rect.Right / RenderTargets.TEX_WIDTH, (float)rect.Top / RenderTargets.TEX_HEIGHT);
//			//screenQuad[2].TextureCoordinate = new Vector2((float)rect.Left / RenderTargets.TEX_WIDTH, (float)rect.Bottom / RenderTargets.TEX_HEIGHT);
//			//screenQuad[3].TextureCoordinate = new Vector2((float)rect.Right / RenderTargets.TEX_WIDTH, (float)rect.Bottom / RenderTargets.TEX_HEIGHT);

//			vBuffer.SetData(screenQuad);
//		}
//		public static void SetTotalSize(Rectangle rect) {

//			screenSize = rect;
//		}

//		public static void DrawOnto(RenderTarget2D renderTo, Effect effect, BlendState blendState, DepthStencilState stencilState = null) {

//			var tech = effect.Techniques["ScreenFilter"];
//			if (tech == null)
//				return;
//			EffectPass pass = null;

//			int index = 4 - (int)Settings.GraphicsLevel;

//			for (; index <= 4; index++) {
//				pass = tech.Passes[PassNames[index]];
//				if (pass != null)
//					break;
//			}
//			if (pass == null) {
//				pass = tech.Passes[0];
//			}

//			if (renderTo != null)
//				Draw.GraphicsDevice.SetRenderTarget(renderTo);

//			if (stencilState == null) {
//				stencilState = DepthState;
//			}

//			Draw.GraphicsDevice.Viewport = new Viewport(viewRect.Left, viewRect.Top, viewRect.Width, viewRect.Height);


//			Draw.GraphicsDevice.BlendState = blendState;
//			Draw.GraphicsDevice.DepthStencilState = stencilState;
//			Draw.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

//			Draw.GraphicsDevice.SetVertexBuffer(vBuffer);
//			Draw.GraphicsDevice.Indices = iBuffer;

//			//Atlases.Effects["screen_effects/base"].CurrentTechnique.Passes[0].Apply();
//			pass.Apply();
//			Draw.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

//		}
//		public static void DrawOntoSprite(RenderTarget2D renderTo, Effect effect, BlendState blendState, Viewport view) {

//			var tech = effect.Techniques["SpriteFilter"];
//			if (tech == null)
//				return;
//			var pass = tech.Passes["Main"];

//			if (pass == null)
//				return;

//			if (renderTo != null)
//				Draw.GraphicsDevice.SetRenderTarget(renderTo);


//			Draw.GraphicsDevice.Viewport = view;

//			Draw.GraphicsDevice.BlendState = blendState;
//			Draw.GraphicsDevice.DepthStencilState = DepthState;
//			Draw.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

//			Draw.GraphicsDevice.SetVertexBuffer(vBuffer);
//			Draw.GraphicsDevice.Indices = iBuffer;

//			pass.Apply();
//			Draw.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

//		}

//		public Effect Effect;
//		public virtual bool RendersToMain => false;
//		public int RenderOrder;
//		public GraphicsSettings LowestSetting = GraphicsSettings.Good;

//		protected float TimeScale = 1;

//		public ScreenFilter() {
//			//RenderOrder = RenderPasses.Final;
//		}

//		protected virtual void SetParameter(Scene scene, EffectParameter parameter) {

//			switch (parameter.Name) {
//				//case "AlbedoPass":
//				//	parameter.SetValue(RenderTargets.Albedo);
//				//	break;
//				//case "NormalPass":
//				//	parameter.SetValue(RenderTargets.Normal);
//				//	break;
//				//case "DepthPass":
//				//	parameter.SetValue(RenderTargets.Depth);
//				//	break;
//				//case "ShadowPass":
//				//	parameter.SetValue(RenderTargets.Shadow);
//				//	break;
//				//case "time":
//				//	parameter.SetValue(scene.TimeActive * TimeScale);
//				//	break;
//				//case "cameraPos": {
//				//	Vector2 camPos = scene.Tracker.GetEntity<Camera>().Position.XZ();
//				//	parameter.SetValue(camPos - GameplayRenderer.CurrentPlayer.CameraLeftover);
//				//	break;
//				//}
//				//case "mouse": {
//				//	Vector2 camPos = MInput.Mouse.RawPosition / new Vector2(Engine.ViewWidth, Engine.ViewHeight);
//				//	parameter.SetValue(camPos);
//				//	break;
//				//}
//				//case "ratio": {

//				//	parameter.SetValue(screenSize.Width / (float)screenSize.Height);

//				//	break;
//				//}
//				//case "ratioScale": {

//				//	parameter.SetValue(new Vector2(
//				//		Math.Max(screenSize.Width / (float)screenSize.Height, 1),
//				//		Math.Max(screenSize.Height / (float)screenSize.Width, 1)));

//				//	break;
//				//}
//				//case "screenOffset": {
//				//	Vector2 camPos = Vector3.Transform(scene.Camera.Position, Camera.OrthoPerfectMatrix).XY();
//				//	camPos = Calc.Round(camPos);

//				//	camPos.Y *= -screenSize.Width / (float)screenSize.Height;

//				//	parameter.SetValue(camPos / screenSize.Width);
//				//	break;
//				//}
//				//case "stableOffset": {
//				//	Vector2 camPos = GameplayRenderer.CurrentPlayer.CameraLeftover;

//				//	camPos.Y *= screenSize.Width / (float)screenSize.Height;

//				//	parameter.SetValue(camPos / screenSize.Width);
//				//	break;
//				//}
//				default:

//					//parameter.SetDefault();
//					//switch (parameter.ParameterType) {
//					//	case EffectParameterType.Bool:
//					//		parameter.SetValue(false);
//					//		break;
//					//	case EffectParameterType.Texture1D:
//					//	case EffectParameterType.Texture3D:
//					//	case EffectParameterType.Texture:
//					//	case EffectParameterType.TextureCube:
//					//		parameter.SetValue((Texture)null);
//					//		break;
//					//	case EffectParameterType.Int32:
//					//		parameter.SetValue(0);
//					//		break;
//					//	case EffectParameterType.Single:
//					//		parameter.SetValue(0f);
//					//		break;
//					//}
//					break;
//			}

//		}
//		public virtual void Update() {

//		}
//		public void DrawOnto(RenderTarget2D renderTo) {

//			DrawOnto(renderTo, Effect, BlendState.Opaque);
//		}

//		public void SetParameters(Scene scene) {
//			foreach (var par in Effect.Parameters) {
//				//par.SetDefault();
//			}
//			foreach (var par in Effect.Parameters) {
//				SetParameter(scene, par);
//			}
//		}

//		public abstract void Render(Scene scene);
//		public virtual void RenderSprite(Scene scene, params RenderTarget2D[] renderTargets) {

//		}
//	}
//}