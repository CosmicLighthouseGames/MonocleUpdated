using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace Monocle {
	public class BasicRenderer : Renderer {

		public BasicRenderer() {
		}


		public override void Render(Scene scene) {

			var graphics = Draw.GraphicsDevice;
			graphics.Clear(Color.Red);


			List<Camera> textureCameras = new List<Camera>(),
				renderCameras = new List<Camera>();

			foreach (Camera cam in scene.Tracker.GetEntities<Camera>()) {
				if (cam.RenderTargets != null)
					textureCameras.Add(cam);
				else
					renderCameras.Add(cam);
			}


			scene.Entities.Render();

			foreach (var cam in textureCameras) {
				cam.RenderCamera();
			}
			foreach (var cam in renderCameras) {
				cam.RenderCamera();
			}

			Draw.ClearGraphics();
		}

	}
}
