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
        public Color Color = Color.White;
        public Matrix? OverrideMatrix;
        public DepthStencilState DepthStencilState;

        public int? Stencil {
            get {
                if (DepthStencilState == null) return null;
                return DepthStencilState.StencilWriteMask;
            }
		}
		public int? StencilMask {
			get {
				if (DepthStencilState == null)
					return null;
				return DepthStencilState.StencilMask;
			}
		}

        void CheckNull() {
			if (DepthStencilState == null) {
				DepthStencilState = new DepthStencilState();
				DepthStencilState.ReadFrom(Draw.DefaultDepthState);
			}

		}

		public void SetStencilWrite(int stencil, DepthStencilState copyFrom = null) {
            CheckNull();

            if (copyFrom != null) {

				DepthStencilState.ReadFrom(copyFrom);
			}

            DepthStencilState.StencilEnable = true;
			DepthStencilState.StencilWriteMask = int.MaxValue;
			DepthStencilState.StencilMask = int.MaxValue;
            DepthStencilState.ReferenceStencil = stencil;
			DepthStencilState.StencilFunction = CompareFunction.Always;
			DepthStencilState.CounterClockwiseStencilFunction = CompareFunction.Always;
			DepthStencilState.StencilPass = StencilOperation.Replace;
		}

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
