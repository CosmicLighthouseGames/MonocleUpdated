using Microsoft.Xna.Framework.Graphics;

namespace Monocle
{
    public class SingleTagRenderer : Renderer
    {
        public BitTag Tag;
        public BlendState BlendState;
        public SamplerState SamplerState;
        public Effect Effect;
        public Camera Camera;

        public SingleTagRenderer(BitTag tag)
        {
            Tag = tag;
            BlendState = BlendState.AlphaBlend;
            SamplerState = SamplerState.LinearClamp;
            Camera = new Camera();
        }

        public override void BeforeRender(Scene scene)
        {

        }

        public override void Render(Scene scene)
        {

        }

        public override void AfterRender(Scene scene)
        {

        }
    }
}
