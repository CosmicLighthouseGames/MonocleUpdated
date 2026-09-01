using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle
{
    public abstract class GraphicsComponent : Component
    {
        public Vector3 Position;
        public Vector2 Origin;
        public Vector3 Scale = Vector3.One;
        public Quaternion Rotation = Quaternion.Identity;
        public Color Color {
            get => Material.Color;
            set => Material.Color = value;
        }
        public Matrix? OverrideMatrix;
        public Material Material;

        public void SetAlpha(float alpha) {
            Material.Color.A = (byte)(alpha * 255);
		}
		public void SetAlpha(byte alpha) {
			Material.Color.A = alpha;
		}
		public void SetAlpha(int alpha) {
			Material.Color.A = (byte)alpha;
		}


		public GraphicsComponent(bool active)
            : base(active, true) {
			Material = Material.DefaultMaterial(Color.White);
		}

        public float X
        {
            get { return Position.X; }
            set { Position.X = value; }
        }

        public float Y
        {
            get { return Position.Y; }
            set { Position.Y = value; }
        }

        public float Z {
            get { return Position.Z; }
            set { Position.Z = value; }
        }

        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
    }
}
