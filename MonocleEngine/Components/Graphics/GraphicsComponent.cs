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
        public Quaternion Rotation;
        public Color Color = Color.White;
        public Matrix? OverrideMatrix;
        public int Stencil;

        public GraphicsComponent(bool active)
            : base(active, true)
        {

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
