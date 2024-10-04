using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Monocle {

	/// <summary>
	/// Code based on t3ssel8r's Procedural animation code, found here: https://youtu.be/KPoeNZZ6H4s?si=_HGxjV3KuPZHid0Y
	/// </summary>
	public class SecondOrderDynamics : Component {

		private Vector3 xp;
		public Vector3 Value, Velocity;
		private float k1, k2, k3;

		public Vector3 Target;

		public SecondOrderDynamics() : base(true, false) {

		}

		public void SetDynamics(float frequency, float damping, float response) {
			k1 = damping / (MathHelper.Pi * frequency);
			k2 = 1 / ((MathHelper.TwoPi * frequency) * (MathHelper.TwoPi * frequency));
			k3 = response * damping / (MathHelper.TwoPi * frequency);
		}

		private void UpdateDynamics(float delta, Vector3 target, Vector3? targetDelta = null) {
			if (targetDelta == null) {
				targetDelta = (target - xp) / delta;
				xp = target;
			}
			Velocity = Velocity + (target + targetDelta.Value * k3 - Value - Velocity * k1) * delta / k2;
			Value = Value + Velocity * delta;
		}

		public override void Update() {


			UpdateDynamics(Engine.DeltaTime, Target);

			base.Update();
		}
	}
}
