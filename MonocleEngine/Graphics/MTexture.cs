﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Monocle
{
    public class MTexture
    {
        static public MTexture FromFile(string filename)
        {
            var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            var texture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, fileStream);
            fileStream.Close();

            return new MTexture(texture);
        }

        public MTexture() { }

        public MTexture(Texture2D texture)
        {
            this.texture = texture;
            AtlasPath = null;
			ClipRect = new Rectangle(0, 0, this.Texture.Width, this.Texture.Height);
            DrawOffset = Vector2.Zero;
            Width = ClipRect.Width;
            Height = ClipRect.Height;
            SetUtil();
        }

		[DebuggerHidden]
		public MTexture(MTexture parent, int x, int y, int width, int height)
        {
            Parent = parent;
            AtlasPath = null;

            ClipRect = parent.GetRelativeRect(x, y, width, height);
            DrawOffset = new Vector2(-Math.Min(x - parent.DrawOffset.X, 0), -Math.Min(y - parent.DrawOffset.Y, 0));
            Width = width;
            Height = height;
            SetUtil();
        }

        [DebuggerHidden]
        public MTexture(MTexture parent, Rectangle clipRect)
            : this(parent, clipRect.X, clipRect.Y, clipRect.Width, clipRect.Height)
        {

        }

        public MTexture(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height)
        {
            texture = parent.Texture;
            AtlasPath = atlasPath;

            ClipRect = parent.GetRelativeRect(clipRect);
            DrawOffset = drawOffset;
            Width = width;
            Height = height;
            SetUtil();
        }

        public MTexture(MTexture parent, string atlasPath, Rectangle clipRect)
            : this(parent, clipRect)
        {
            AtlasPath = atlasPath;
        }

        public MTexture(Texture2D texture, Vector2 drawOffset, int frameWidth, int frameHeight)
        {
            this.texture = texture;
            ClipRect = new Rectangle(0, 0, texture.Width, texture.Height);
            DrawOffset = drawOffset;
            Width = frameWidth;
            Height = frameHeight;
            SetUtil();
        }

        public MTexture(int width, int height, Color color)
        {
            texture = new Texture2D(Engine.Instance.GraphicsDevice, width, height);
            var colors = new Color[width * height];
            for (int i = 0; i < width * height; i++)
                colors[i] = color;
            texture.SetData<Color>(colors);
            

            ClipRect = new Rectangle(0, 0, width, height);
            DrawOffset = Vector2.Zero;
            Width = width;
            Height = height;
            SetUtil();
        }

		[DebuggerHidden]
		private void SetUtil()
        {
            Center = new Vector2(Width, Height) * 0.5f;
            LeftUV = ClipRect.Left / (float)Texture.Width;
            RightUV = ClipRect.Right / (float)Texture.Width;
            TopUV = ClipRect.Top / (float)Texture.Height;
            BottomUV = ClipRect.Bottom / (float)Texture.Height;
        }

        public void Unload()
        {
			texture.Dispose();
			texture = null;
        }
       
        public MTexture GetSubtexture(int x, int y, int width, int height, MTexture applyTo = null)
        {
            if (applyTo == null)
                return new MTexture(this, x, y, width, height);
            else
            {
                applyTo.Parent = this;
                applyTo.AtlasPath = null;

                applyTo.ClipRect = GetRelativeRect(x, y, width, height);
                applyTo.DrawOffset = new Vector2(-Math.Min(x - DrawOffset.X, 0), -Math.Min(y - DrawOffset.Y, 0));
                applyTo.Width = width;
                applyTo.Height = height;
                applyTo.SetUtil();

                return applyTo;
            }
        }

        [DebuggerHidden]
        public MTexture GetSubtexture(Rectangle rect)
        {
            return new MTexture(this, rect);
        }

        public void Dispose()
        {
            Texture.Dispose();
        }

        public void SetTexture(Texture2D texture) {
            this.texture = texture;
			ClipRect = new Rectangle(0, 0, this.Texture.Width, this.Texture.Height);
        }

        #region Properties


        public Texture2D Texture { get { return Parent == null ? texture : Parent.Texture; } }
        public MTexture Parent { get; private set; }

        Texture2D texture;

        
        public Rectangle ClipRect { get; private set; }
        public string AtlasPath { get; private set; }
        public Vector2 DrawOffset { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector2 Center { get; private set; }
        public float LeftUV { get; private set; }
        public float RightUV { get; private set; }
        public float TopUV { get; private set; }
        public float BottomUV { get; private set; }

        #endregion

        #region Helpers

        public override string ToString()
        {
            if (AtlasPath != null)
                return AtlasPath;
            else
                return "MTexture [" + Texture.Width + " x " + Texture.Height + "]";
        }

		[DebuggerHidden]
		public Rectangle GetRelativeRect(Rectangle rect)
        {
            return GetRelativeRect(rect.X, rect.Y, rect.Width, rect.Height);
        }

		[DebuggerHidden]
		public Rectangle GetRelativeRect(int x, int y, int width, int height)
        {
            int atX = (int)(ClipRect.X - DrawOffset.X + x);
            int atY = (int)(ClipRect.Y - DrawOffset.Y + y);

            int rX = (int)MathHelper.Clamp(atX, ClipRect.Left, ClipRect.Right);
            int rY = (int)MathHelper.Clamp(atY, ClipRect.Top, ClipRect.Bottom);
            int rW = Math.Max(0, Math.Min(atX + width, ClipRect.Right) - rX);
            int rH = Math.Max(0, Math.Min(atY + height, ClipRect.Bottom) - rY);

            return new Rectangle(rX, rY, rW, rH);
        }
        

        public int TotalPixels
        {
            get { return Width * Height; }
        }

        #endregion

        #region Draw

        public void Draw(Vector3 position) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, Vector2.Zero, Vector2.One, Quaternion.Identity, Color.White);
		}
		public void Draw(Vector3 position, Vector2 origin) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, Vector2.One, Quaternion.Identity, Color.White);
		}
		public void Draw(Vector3 position, Vector2 origin, Color color) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, Vector2.One, Quaternion.Identity, color);
		}
		public void Draw(Vector3 position, Vector2 origin, Color color, float scale) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, Vector2.One * scale, Quaternion.Identity, color);
		}
		public void Draw(Vector3 position, Vector2 origin, Color color, float scale, Quaternion rotation) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, Vector2.One * scale, rotation, color);
        }

		public void Draw(Vector3 position, Vector2 origin, Color color, Vector2 scale) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, scale, Quaternion.Identity, color);
		}
		public void Draw(Vector3 position, Vector2 origin, Color color, Vector2 scale, Quaternion rotation) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, origin, scale, rotation, color);
		}

		public void Draw(Vector3 position, Color color, Vector2 scale) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, Vector2.Zero, scale, Quaternion.Identity, color);
        }
        public void Draw(Vector3 position, Color color) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, Vector2.Zero, Vector2.One, Quaternion.Identity, color);
        }
        public void Draw(Vector3 position, Quaternion rotation, Color color) {
#if DEBUG
			if (Texture.IsDisposed)
				throw new Exception("Texture2D Is Disposed");
#endif
			Monocle.Draw.Texture(this, position, Vector2.Zero, Vector2.One, rotation, color);
        }

        #endregion
    }
}
