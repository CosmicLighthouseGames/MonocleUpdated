using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;


namespace Monocle {
	public struct RectangleF {
		public float X, Y, Width, Height;

		public float Left => X;
		public float Top => Y;
		public float Right => X + Width;
		public float Bottom => Y + Height;

		public RectangleF() {
			X = 0;
			Y = 0;
			Width = 0;
			Height = 0;
		}
		public RectangleF(float x, float y, float width, float height) {
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}
		public RectangleF(Vector2 position, Vector2 size) {
			X = position.X;
			Y = position.Y;
			Width = size.X;
			Height = size.Y;
		}

		public bool Intersects(RectangleF rect) {
			return rect.Right > Left && rect.Left < Right && rect.Top > Bottom && rect.Bottom < Top;
		}
		public bool Contains(RectangleF rect) {
			return rect.Left > Left && rect.Right < Right && rect.Top > Top && rect.Bottom < Bottom;
		}
		public bool Contains(Vector2 point) {
			return point.X > Left && point.X  < Right && point.Y > Top && point.Y < Bottom;
		}
		public bool Contains(float x, float y) {
			return x > Left && x  < Right && y > Top && y < Bottom;
		}

		public void Inflate(float amount) {
			X -= amount;
			Y -= amount;
			Width += amount * 2;
			Height += amount * 2;
		}
		public void Inflate(float horizontalAmount, float verticalAmount) {
			X -= horizontalAmount;
			Y -= verticalAmount;
			Width += horizontalAmount * 2;
			Height += verticalAmount * 2;
		}
	}
}
