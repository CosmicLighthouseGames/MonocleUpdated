﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using static System.Net.Mime.MediaTypeNames;

namespace Monocle
{
	public class Image : GraphicsComponent
	{
		public MTexture Texture;

		public float? PixelsPerUnit;

		static Image() {

		}

		public Material material;

		public Image(MTexture texture)
			: base(false)
		{
			Texture = texture;
		}

		internal Image(MTexture texture, bool active)
			: base(active)
		{
			Texture = texture;
		}
		public override void EntityAwake() {
			base.EntityAwake();
		}
		public override void Update() {
			base.Update();
		}

		public override void Render() {
			Vector3 pos = Position;
			if (Entity != null)
				pos += Entity.Position;

			RenderAt(pos);
		}
		public void RenderAt(Vector3 pos) {
			if (Texture != null) {

				Matrix matrix;

				if (OverrideMatrix.HasValue) {

					matrix = OverrideMatrix.Value;
				}
				else {
					float ppu = PixelsPerUnit??Engine.PixelsPerUnit;

					matrix = Matrix.Identity
						* Matrix.CreateTranslation(new Vector3(-Origin.X, -Origin.Y, 0))
						* Matrix.CreateScale(Scale.X / ppu, Scale.Y / ppu, 1)
						* Matrix.CreateFromQuaternion(Rotation)
						* Matrix.CreateTranslation(pos.X, pos.Y, pos.Z);


				}

				Draw.Texture(Texture, matrix, Color, Stencil, (FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (FlipY ? SpriteEffects.FlipVertically : SpriteEffects.None), material);
			}
		}
		public void Render9Slice(Vector2 point, Point size, Rectangle center) {
			if (Texture != null) {

				Matrix matrix;

				//if (GameplayRenderer.CurrentPass == RenderingPass.ScreenUI) {

				//	Rectangle clip = new Rectangle(0, 0, 0, center.Y);
				//	Vector2 pos = new Vector2(point.X, point.Y);
				//	Vector2 scale = new Vector2(1, 1);
				//	for (int y = 0; y < 3; y++) {

				//		switch (y) {
				//			case 1:
				//				pos.Y += center.Y;
				//				clip.Y = center.Y;
				//				clip.Height = center.Height;
				//				scale.Y = (size.Y - Texture.Height) / center.Height;
				//				break;
				//			case 2:
				//				pos.Y = (point.Y + size.Y) - center.Bottom;
				//				clip.Y = center.Bottom;
				//				clip.Height = Texture.Height - center.Bottom;
				//				scale.Y = 1;
				//				break;
				//		}
				//		for (int x = 0; x < 3; x++) {
				//			switch (x) {
				//				case 0:
				//					pos.X = point.X;
				//					clip.X = 0;
				//					clip.Width = center.X;
				//					break;
				//				case 1:
				//					pos.X += center.X;
				//					clip.X = center.X;
				//					clip.Width = center.Width;
				//					scale.X = (size.X - Texture.Width) / center.Width;
				//					break;
				//				case 2:
				//					pos.X = (point.X + size.X) - center.Right;
				//					clip.X = center.Right;
				//					clip.Width = Texture.Width - center.Right;
				//					scale.X = 1;
				//					break;
				//			}

				//			Draw.SpriteBatch.Draw(Texture.Texture, new Vector2(pos.X, pos.Y), clip, Color, 0, Origin, scale, SpriteEffects.None, Draw.RealDepth);

				//		}
				//	}

				//}
				//else {

				//	throw new NotImplementedException();
				//}
			}
		}

		public void DrawSubrect(Vector3 offset, Rectangle rectangle) {
			if (Texture != null) {
				var clipOffset = new Vector2(-Math.Min(rectangle.X - Texture.DrawOffset.X, 0), -Math.Min(rectangle.Y - Texture.DrawOffset.Y, 0));
				var ogTex = Texture;
				Vector3 ogPos = Position;
				Position += offset;

				//while (Texture.Parent != null) {
				//	Texture = Texture.Parent;
				//}
				Texture = Texture.GetSubtexture(new Rectangle((int)(clipOffset.X + rectangle.X), (int)(clipOffset.Y + rectangle.Y), rectangle.Width, rectangle.Height));

				Render();
				Position = ogPos;
				Texture = ogTex;

				//Draw.SpriteBatch.Draw(Texture.Texture, RenderPosition + offset, clip, Color, Rotation, Origin - clipOffset, Scale, Effects, 0);
			}
		}

		public virtual float Width
		{
			get { return Texture.Width; }
		}

		public virtual float Height
		{
			get { return Texture.Height; }
		}

		public Image SetOrigin(float x, float y)
		{
			Origin.X = x;
			Origin.Y = y;
			return this;
		}

		public Image CenterOrigin()
		{
			SetOrigin(Width / 2f, Height / 2f);

			return this;
		}

		public Image JustifyOrigin(Vector2 at) {
			SetOrigin(Width * at.X, Height * at.Y);

			return this;
		}

		public Image JustifyOrigin(float x, float y) {
			SetOrigin(Width * x, Height * y);

			return this;
		}
	}
}
