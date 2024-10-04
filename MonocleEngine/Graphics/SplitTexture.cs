using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Monocle {
    public class SplitTexture {
        MTexture[,] split;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public SplitTexture(MTexture _tex, Rectangle _center) {

            Width = _tex.Width;
            Height = _tex.Height;

            split = new MTexture[3, 3];

            for (int x = 0; x < 3; ++x) {
                for (int y = 0; y < 3; ++y) {
                    Rectangle rect = new Rectangle(x > 0 ? (x + _center.Left - 1) : 0, y > 0 ? (y + _center.Top - 1) : 0, x == 1 ? 1 : (x == 0 ? _center.Left : Width - _center.Right), y == 1 ? 1 : (y == 0 ? _center.Top : Height - _center.Bottom));
                    split[x, y] = _tex.GetSubtexture(rect);
                }
            }
        }
        public void Render(Rectangle _rect, Vector2 _scale) {
            Render(_rect, _scale, Color.White);
        }

        public void Render(Rectangle _rect, Vector2 _scale, Color _color) {


            for (int x = 0; x < 3; ++x) {
                for (int y = 0; y < 3; ++y) {

                    Vector2 scale = new Vector2(_rect.Width, _rect.Height);
                    Vector3 pos = new Vector3(_rect.X, _rect.Y, 0);

                    if (x != 1) {
                        if (x == 2)
                            pos.X += _rect.Width - (split[2, 2].Width * _scale.X);

                        scale.X = _scale.X;
                    }
                    else {
                        pos.X += split[0, 0].Width * _scale.X;
                        scale.X -= (split[0, 0].Width + split[2, 2].Width) * _scale.X;
                    }

                    if (y != 1) {
                        if (y == 2)
                            pos.Y += _rect.Height - (split[2, 2].Height * _scale.Y);

                        scale.Y = _scale.Y;
                    }
                    else {
                        pos.Y += split[0, 0].Height * _scale.Y;
                        scale.Y -= (split[0, 0].Height + split[2, 2].Height) * _scale.Y;
                    }

                    split[x, y].Draw(pos, Vector2.Zero, _color, scale);
                }
            }

        }
    }
}
