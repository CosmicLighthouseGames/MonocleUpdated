//#define CONST
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Reflection;
using System.Runtime;

using System.Diagnostics;
using Microsoft.Xna.Framework.Input;
using System.Threading;


namespace Monocle {
	public class Engine : Game {
		public string Title;
		public Version Version;

		// references
		public static Engine Instance { get; private set; }
		public static GraphicsDeviceManager Graphics { get; private set; }
		public static Commands Commands { get; private set; }
		public static Events Events { get; private set; }
		public static Pooler Pooler { get; private set; }
		public static Action OverloadGameLoop;
		public static CoroutineHolder CoroutineList { get; internal set; }


		public static bool ShowFPS { get; set; }

		// screen size
		public static int PixelsPerUnit { get; set; } = 1;
		public static float UnitWidth { get; private set; }
		public static float UnitHeight { get; private set; }
		public static int ViewWidth { get; private set; }
		public static int ViewHeight { get; private set; }
		public static int WindowWidth { get; private set; }
		public static int WindowHeight { get; private set; }
		public static bool UseWindowSize {
			get { return useWindowSize; }
			set {
				useWindowSize = value;
				Instance.UpdateView();
			}
		}
		public static int ViewPadding {
			get { return viewPadding; }
			set {
				viewPadding = value;
				Instance.UpdateView();
			}
		}
		private static int viewPadding = 0;
		private static bool resizing, useWindowSize = false;

		// time
		public static float DeltaTime => RawDeltaTime * TimeRate;
		public static float DeltaTimeRate => Calc.Snap(DeltaTime * 60, 0.001f);

		public static float RawDeltaTime { get; private set; }
		public static float TimeActive { get; private set; }
		public static float RealTimeActive { get; private set; }
		public static float TimeRate = 1f;
		public static float FreezeTimer;
		public static int FPS;
		static int FPS_Value;
		private TimeSpan counterElapsed = TimeSpan.Zero;
		private int fpsCounter = 0;

#if DEBUG
		public static Keys? PauseKey, FrameKey, SuperSpeedKey;
		public static float DebugSpeed = 1;
		static float DebugWait = 0;
#endif

		private Point initializedSize;

		// content directory
#if !CONSOLE
		private static string AssemblyDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#endif

		public static string ContentDirectory {
#if PS4
			get { return Path.Combine("/app0/", Instance.Content.RootDirectory); }
#elif NSWITCH
			get { return Path.Combine("rom:/", Instance.Content.RootDirectory); }
#elif XBOXONE
			get { return Instance.Content.RootDirectory; }
#else
			get { return Path.Combine(AssemblyDirectory, Instance.Content.RootDirectory); }
#endif
		}

		// util
		public static Color ClearColor;
		public static bool ExitOnEscapeKeypress;

		// scene
		private Scene scene;
		private Scene nextScene;

		public static float UpdateFrameData;

		public Engine(float width, float height, int windowWidth, int windowHeight, string windowTitle, bool fullscreen) {
			Instance = this;

			base.IsFixedTimeStep = true;

			Title = windowTitle;
			UnitWidth = width;
			UnitHeight = height;
			initializedSize = new Point(windowWidth, windowHeight);
			ClearColor = Color.Black;

			Graphics = new GraphicsDeviceManager(this);
			Graphics.DeviceReset += OnGraphicsReset;
			Graphics.DeviceCreated += OnGraphicsCreate;
			Graphics.SynchronizeWithVerticalRetrace = true;
			Graphics.PreferMultiSampling = false;
			Graphics.GraphicsProfile = GraphicsProfile.Reach;
			Graphics.PreferredBackBufferFormat = SurfaceFormat.Color;
			Graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;

#if PS4 || XBOXONE
			Graphics.PreferredBackBufferWidth = 1920;
			Graphics.PreferredBackBufferHeight = 1080;
#elif NSWITCH
			Graphics.PreferredBackBufferWidth = 1280;
			Graphics.PreferredBackBufferHeight = 720;
#else
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnClientSizeChanged;

			if (fullscreen) {
				Graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
				Graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
				Graphics.IsFullScreen = true;
			}
			else {
				Graphics.PreferredBackBufferWidth = windowWidth;
				Graphics.PreferredBackBufferHeight = windowHeight;
				Graphics.IsFullScreen = false;
			}
#endif

			Content.RootDirectory = @"Content";

			IsMouseVisible = false;
			ExitOnEscapeKeypress = true;

			GCSettings.LatencyMode = GCLatencyMode.LowLatency;
		}

#if !CONSOLE
		protected virtual void OnClientSizeChanged(object sender, EventArgs e) {
			if (Window.ClientBounds.Width > 0 && Window.ClientBounds.Height > 0 && !resizing) {
				resizing = true;

				Graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
				Graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
				UpdateView();

				if (scene != null)
					scene.HandleGraphicsReset();
				if (nextScene != null && nextScene != scene)
					nextScene.HandleGraphicsReset();

				resizing = false;
			}
		}
#endif

		protected virtual void OnGraphicsReset(object sender, EventArgs e) {
			UpdateView();

			if (scene != null)
				scene.HandleGraphicsReset();
			if (nextScene != null && nextScene != scene)
				nextScene.HandleGraphicsReset();
			
		}

		protected virtual void OnGraphicsCreate(object sender, EventArgs e) {
			UpdateView();

			if (scene != null)
				scene.HandleGraphicsCreate();
			if (nextScene != null && nextScene != scene)
				nextScene.HandleGraphicsCreate();
		}

		protected override void OnActivated(object sender, EventArgs args) {
			base.OnActivated(sender, args);

			if (scene != null)
				scene.GainFocus();
		}

		protected override void OnDeactivated(object sender, EventArgs args) {
			base.OnDeactivated(sender, args);

			if (scene != null)
				scene.LoseFocus();
		}

		protected override void Initialize() {
			Tracker.Initialize();
			AssetLoader.Initialize();

			Graphics.PreferredBackBufferWidth = initializedSize.X;
			Graphics.PreferredBackBufferHeight = initializedSize.Y;
			Graphics.ApplyChanges();

			UpdateView();

			base.Initialize();

			MInput.Initialize();
			Pooler = new Pooler();
			Commands = new Commands();
			Events = new Events();
			CoroutineList = new CoroutineHolder();
		}

		protected override void LoadContent() {
			base.LoadContent();

			Monocle.Draw.Initialize(GraphicsDevice);
		}


		float lastFrame = 0;

		protected override void Update(GameTime gameTime) {


			RealTimeActive += (float)gameTime.ElapsedGameTime.TotalSeconds;

			lastFrame += (float)gameTime.ElapsedGameTime.TotalSeconds;

			//if (lastFrame < 0.016f) {
			//	Thread.Sleep(5);
			//	return;
			//}
			RawDeltaTime = lastFrame;
			TimeActive += RawDeltaTime;
			lastFrame = 0;

			//Update input
			MInput.Update(true);

#if !CONSOLE
			if (ExitOnEscapeKeypress && MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.Escape)) {
				Exit();
				return;
			}
#endif


			//Debug Console
			if (Commands.Open || Commands.TempOpen > 0)
				Commands.UpdateOpen();
			else if (Commands.Enabled)
				Commands.UpdateClosed();

			if (OverloadGameLoop != null) {
				OverloadGameLoop();
				base.Update(gameTime);
				return;
			}

#if DEBUG
			float tempSpeed = DebugSpeed;
			if (SuperSpeedKey != null && MInput.Keyboard.Check(SuperSpeedKey.Value)) {
				tempSpeed = 10;
			}
			//tempSpeed = 10;

			if (tempSpeed >= 1 || DebugWait >= 1 || PauseKey == null || (FrameKey != null && MInput.Keyboard.Pressed(FrameKey.Value))) {
				for (int i = 0; i < Math.Max(1, tempSpeed); i++) {
#endif
					//Changing scenes
					if (scene != nextScene) {
						var lastScene = scene;
						if (scene != null) {
							scene.End();
						}
						scene = nextScene;
						OnSceneTransition(lastScene, nextScene);
						if (scene != null) {
							scene.Begin();

							scene.RendererList.UpdateLists();
						}
					}

					CoroutineList.Update();
					if (scene != null) {
						scene.BeforeUpdate();
						scene.Update();
						scene.AfterUpdate();
					}
					//Update current scene
					if (FreezeTimer > 0)
						FreezeTimer = Math.Max(FreezeTimer - RawDeltaTime, 0);

#if DEBUG
				}
				if ((PauseKey != null && MInput.Keyboard.Pressed(PauseKey.Value)) ||
					(FrameKey != null && MInput.Keyboard.Pressed(FrameKey.Value))) {
					DebugSpeed = 0;
				}
				DebugWait = 0;
			}
			else {
				if (MInput.Keyboard.Pressed(PauseKey.Value)) {
					DebugSpeed = 1;
				}
			}
			if (MInput.Mouse.WheelDelta > 0) {
				DebugSpeed = Math.Max(DebugSpeed, 1.0f / 128);
				DebugSpeed = Math.Clamp(DebugSpeed / 0.75f, 0, 1);
			}
			if (MInput.Mouse.WheelDelta < 0) {
				DebugSpeed = Math.Clamp(DebugSpeed * 0.75f, 0, 1);
				DebugSpeed = Math.Max(DebugSpeed, 1.0f / 128);
			}
			DebugWait += DebugSpeed;
#endif

			base.Update(gameTime);
		}

		float lastFrameRender = 0;
		protected override void Draw(GameTime gameTime) {

			Monocle.Draw.UpdatePerFrame();

			RenderCore();

			base.Draw(gameTime);
			if (Commands.Open || Commands.TempOpen > 0)
				Commands.Render();

			//Frame counter
			fpsCounter++;
			counterElapsed += gameTime.ElapsedGameTime;
			if (counterElapsed >= TimeSpan.FromSeconds(1)) {

				Window.Title = Title;

				FPS = fpsCounter;
				fpsCounter = 0;
				counterElapsed -= TimeSpan.FromSeconds(1);
			}
			if (ShowFPS) {
				string name = FPS.ToString() + " fps";

				Color color = Color.LightGreen;

				if (FPS < 59) {
					color = Color.Red;
				}

				float w = WindowWidth / PixelsPerUnit,
					h = WindowHeight / PixelsPerUnit;

				Monocle.Draw.ClearGraphics(w, h);

				Vector2 size = Monocle.Draw.DefaultFont.MeasureString(name);
				float y = h - (size.Y + 0.1f);

				Monocle.Draw.Rect(w - size.X - 0.4f, y, size.X + 0.4f, size.Y + 0.1f, Color.Black);
				Monocle.Draw.DefaultFont.Draw(name, new Vector3(w - size.X - 0.1f, y - 0.05f, 0), color);

				name = (GC.GetTotalMemory(false) / 1048576f).ToString("F") + " MB";
				size = Monocle.Draw.DefaultFont.MeasureString(name);
				y -= (size.Y + 0.1f);
				Monocle.Draw.Rect(w - size.X - 0.4f, y, size.X + 0.4f, size.Y + 0.1f, Color.Black);
				Monocle.Draw.DefaultFont.Draw(name, new Vector3(w - size.X - 0.1f, y, 0), color);


				Monocle.Draw.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
				Monocle.Draw.GraphicsDevice.DepthStencilState = DepthStencilState.None;
				Monocle.Draw.RenderPass();
				Monocle.Draw.ClearGraphics();
				//
				//Window.Title = Title + " " + fpsCounter.ToString() + " fps - " + (GC.GetTotalMemory(false) / 1048576f).ToString("F") + " MB";
			}

			Monocle.Draw.ClearGraphics();
			GraphicsDevice.SetRenderTarget(null);
			GC.Collect();

		}

		/// <summary>
		/// Override if you want to change the core rendering functionality of Monocle Engine.
		/// By default, this simply sets the render target to null, clears the screen, and renders the current Scene
		/// </summary>
		protected virtual void RenderCore() {

			if (scene != null)
				scene.BeforeRender();

			if (scene != null) {
				scene.Render();
				scene.AfterRender();
			}

		}

		protected override void OnExiting(object sender, EventArgs args) {
			base.OnExiting(sender, args);
			MInput.Shutdown();
		}

		//public void RunWithLogging() {
		//	try {
		//		Run();
		//	}
		//	catch (Exception e) {
		//		ErrorLog.Write(e);
		//		ErrorLog.Open();
		//	}
		//}

		public static void LockGraphicsDevice(Action onUnlocked) {

			//while (RenderingAttempt) {
			//	Thread.Sleep(1);
			//}

			//while (!Monitor.TryEnter(Instance.GraphicsDevice)) {

			//}

			onUnlocked();

			//Monitor.Exit(Instance.GraphicsDevice);
		}

		#region Scene

		/// <summary>
		/// Called after a Scene ends, before the next Scene begins
		/// </summary>
		protected virtual void OnSceneTransition(Scene from, Scene to) {
			//GC.Collect();
			//GC.WaitForPendingFinalizers();

			TimeRate = 1f;
		}

		/// <summary>
		/// The currently active Scene. Note that if set, the Scene will not actually change until the end of the Update
		/// </summary>
		public static Scene CurrentScene {
			get { return Instance.scene??Instance.nextScene; }
		}

		/// <summary>
		/// The current Scene after the update
		/// </summary>
		public static Scene NextScene {
			get { return Instance.nextScene; }
			set { Instance.nextScene = value; }
		}

		#endregion

		#region Screen

		public static Viewport Viewport { get; private set; }
		public static Matrix ScreenMatrix, MouseMatrix;
		public static float Scaling { get; private set; }
		public static float InvertScaling { get; private set; }

		public static void SetWindowed(int width, int height) {
#if !CONSOLE
			if (width > 0 && height > 0) {
				resizing = true;
				Graphics.PreferredBackBufferWidth = width;
				Graphics.PreferredBackBufferHeight = height;
				Graphics.IsFullScreen = false;
				Graphics.ApplyChanges();
				resizing = false;
			}
#endif
		}

		public static void SetFullscreen() {
#if !CONSOLE
			resizing = true;
			Graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
			Graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
			Graphics.IsFullScreen = true;
			Graphics.ApplyChanges();
			resizing = false;
#endif
		}

		protected virtual void UpdateView() {

			float screenWidth = Graphics.PreferredBackBufferWidth;
			float screenHeight = Graphics.PreferredBackBufferHeight;

			WindowWidth = (int)screenWidth;
			WindowHeight = (int)screenHeight;

			// get View Size
			if (screenWidth / UnitWidth > screenHeight / UnitHeight) {
				ViewWidth = (int)(screenHeight / UnitHeight * UnitWidth);
				ViewHeight = (int)screenHeight;
			}
			else {
				ViewWidth = (int)screenWidth;
				ViewHeight = (int)(screenWidth / UnitWidth * UnitHeight);
			}

			int w = useWindowSize ? WindowWidth : ViewWidth;
			int h = useWindowSize ? WindowHeight : ViewHeight;

			// apply View Padding
			var aspect = ViewHeight / (float)ViewWidth;
			ViewWidth -= ViewPadding * 2;
			ViewHeight -= (int)(aspect * ViewPadding * 2);

			Scaling = Math.Min(w / (float)UnitWidth, h / (float)UnitHeight);
			InvertScaling = (float)UnitWidth / w;

			// update screen matrix
			ScreenMatrix = Matrix.CreateScale(w / (float)UnitWidth, w / (float)UnitWidth, 1);
			MouseMatrix = Matrix.CreateTranslation(((int)screenWidth - w) >> 1, ((int)screenHeight - h) >> 1, 0);

			// update viewport
			Viewport = new Viewport {
				X = (int)(screenWidth / 2 - w / 2),
				Y = (int)(screenHeight / 2 - h / 2),
				Width = w,
				Height = h,
				MinDepth = 0,
				MaxDepth = 1
			};

			scene?.HandleGraphicsReset();
			
			//Debug Log
			//Calc.Log("Update View - " + screenWidth + "x" + screenHeight + " - " + viewport.Width + "x" + viewport.GuiHeight + " - " + viewport.X + "," + viewport.Y);
		}

		#endregion
	}
}
