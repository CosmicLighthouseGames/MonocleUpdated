using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public class PrepRenderPass : GraphicsComponent {

		public Action OnRender;

		public PrepRenderPass(int RenderOrder) : base(true) {
			this.RenderOrder = RenderOrder;
		}

		public override void Render() {
			base.Render();

			Draw.CustomDrawCall(new PriorityDrawCall() {
				OnRender = OnRender,
				RenderOrder = RenderOrder,
			});

		}
	}
}
