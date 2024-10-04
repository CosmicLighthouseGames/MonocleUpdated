using System.IO;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle {
	public class EffectAtlas {
		Dictionary<string, Effect> effects;

		public Effect this[string s] {
			get => effects[s];
		}

		public static EffectAtlas FromDirectory(string path) {
			EffectAtlas effectAtlas = new EffectAtlas();
			effectAtlas.effects = new Dictionary<string, Effect>();

			foreach (var item in AssetLoader.GetContentInFolder(path)) {
				if (item.Extention != ".cso")
					continue;

				string local = Path.ChangeExtension(Path.GetRelativePath(path, item.Path).Replace('\\', '/'), null);

				Engine.LockGraphicsDevice(() => {
					effectAtlas.effects.Add(local, new Effect(Engine.Graphics.GraphicsDevice, item.GetBinary()));
				});
			}

			return effectAtlas;
		}
	}
}
