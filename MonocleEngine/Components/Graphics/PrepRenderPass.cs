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


		public int RenderOrder { get; set; }

		public PrepRenderPass(int renderOrder) : base(true) {
			RenderOrder = renderOrder;
		}

		public override void Render() {
			base.Render();

			Draw.CustomDrawCall(new PriorityDrawCall() {
				OnRender = OnRender,
				RenderOrder = RenderOrder
			});

		}
	}
}
