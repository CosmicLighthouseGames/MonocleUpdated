using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Monocle {
	public class SplitWall : GraphicsComponent {

		Sprite[] textures;

		public new Vector3 RenderPosition {
			get {
				if (Entity != null)
					return Entity.Position + pos;
				else
					return offset;
			}
			set {
				if (Entity != null)
					pos = value - Entity.Position;
				else
					pos = value;
			}
		}
		private Vector3 pos;
		Vector3 offset;

		public SplitWall(Atlas _atlas, string _folder, int size)
			: base(true) {
			RenderPosition = Vector3.Zero;
			offset = new Vector3(16, 20, 0);

			List<Sprite> left = new List<Sprite>();

			left.Add(new Sprite(_atlas, _folder));
			left[0].Add("idle", "left");
			left[0].Play("idle");

			textures = new Sprite[size];

			for (int i = 0; i < size; ++i) {
				
				textures[i] = left[0];
			}

			foreach (var t in textures) {
				t.SetOrigin(0, (int)(t.Height - (t.Width / 2)));
			}
		}
		public SplitWall(Sprite[] _textures)
			: this(true, _textures) {
		}
		public SplitWall(bool _active, Sprite[] _textures)
			: base(_active) {

			RenderPosition = Vector3.Zero;
			offset = new Vector3(16, 10, 0);

			textures = _textures;

			foreach (var t in textures) {
				t.SetOrigin(0, (int)(t.Height - (t.Width / 2)));
			}
		}

		public override void Render() {
			base.Render();

			for (int i = 0; i < textures.Length; ++i) {
				Vector3 pos = RenderPosition + (offset * (i));
				pos.Y = RenderPosition.Y - 15;

				pos.Y = RenderPosition.Y;

				//textures[i].Position = pos.WorldToScreenOrtho2D();
				textures[i].Render();
			}
		}
	}
}
