using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace MonocleTest {
	public class Game1 : Engine {

		Atlas sprites;

		public Game1() : base(1280, 720, 1280, 720, "AAAAAAA", false) {
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
		}

		protected override void LoadContent() {
			base.LoadContent();

			sprites = Atlas.FromAssetHandler("sprites");
		}

		protected override void Initialize() {
			base.Initialize();

			Scene test = new Scene();
			test.Add(new BasicRenderer());

			test.HelperEntity.Add(new Image(sprites["test"]) { Scale = new Vector3(1, 1, 1)});

			Camera testCam = new Camera();
			test.Add(testCam);
			NextScene = test;
		}

		protected override void Update(GameTime gameTime) {


			base.Update(gameTime);
		}

	}
}
