using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using MonoGame;
using YamlDotNet.Core.Tokens;
using System.Diagnostics;

namespace Monocle
{
	public class Atlas
	{
		private Dictionary<string, MTexture> textures = new Dictionary<string, MTexture>(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, List<MTexture>> orderedTexturesCache = new Dictionary<string, List<MTexture>>();

		[DebuggerHidden]
		public static Texture2D TextureFromStream(BinaryReader reader) {

			var graphics = Engine.Instance.GraphicsDevice;

			byte meta = reader.ReadByte();


			int width = reader.ReadUInt16(),
				height = reader.ReadUInt16();

			Color[] colors = new Color[reader.ReadByte()];
			Color[] image = new Color[width * height];

			for (int i = 0; i < colors.Length; ++i) {
				colors[i] = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
			}

			for (int i = 0; i < image.Length; ++i) {
				image[i] = colors[reader.ReadByte()];
			}

			Texture2D texture = new Texture2D(graphics, width, height);
			texture.SetData(image);
			

			return texture;
		}

		public static Atlas FromAssetLoader(string contentFolder) {

			contentFolder = contentFolder.Replace('\\', '/');

			var atlas = new Atlas();
			atlas.textures = new Dictionary<string, MTexture>();

			var graphics = Engine.Instance.GraphicsDevice;

			if (AssetLoader.HasContent($"{contentFolder}.bin")) {

				var content = AssetLoader.GetContent($"{contentFolder}.bin");

				var reader = new BinaryReader(content.ContentStream);

				while (reader.BaseStream.Position < reader.BaseStream.Length) {
					string key = reader.ReadString();

					atlas.textures.Add(key, new MTexture(TextureFromStream(reader)));
				}
			}

			foreach (var item in AssetLoader.GetContentInFolder(contentFolder)) {

				Texture2D texture = null;
				string filepath = null;

				switch (item.Extention) {
					case ".png":
					case ".xnb": {
						

						// make nice for dictionary
						filepath = Path.ChangeExtension(item.Path, null);
						filepath = filepath.Replace('\\', '/');
						filepath = filepath.Substring(contentFolder.Length + 1);

						if (atlas.textures.ContainsKey(filepath))
							break;
						Engine.LockGraphicsDevice(() => {
							texture = Texture2D.FromStream(graphics, item.ContentStream);
						});
					}
						break;
					default:
						continue;
				}
				if (texture == null)
					continue;

				// load
				atlas.textures.Add(filepath, new MTexture(texture));
			}

			AssetLoader.OnAssetUpdatedEvent(contentFolder, (s) => {

				var item = AssetLoader.GetContent(s);
				Texture2D tex = null;
				Engine.LockGraphicsDevice(() => {
					tex = Texture2D.FromStream(graphics, item.ContentStream);
				});

				var filepath = Path.ChangeExtension(item.Path, null);
				filepath = filepath.Replace('\\', '/');
				filepath = filepath.Substring(contentFolder.Length + 1);

				atlas.textures[s] = new MTexture(tex);
			});

			return atlas;
		}

		public MTexture this[string id]
		{
			[DebuggerHidden]
			get { return textures[id]; }
			set { textures[id] = value; }
		}

		public bool Has(string id)
		{
			return textures.ContainsKey(id);
		}

		public MTexture GetOrDefault(string id, MTexture defaultTexture)
		{
			if (String.IsNullOrEmpty(id) || !Has(id))
				return defaultTexture;
			return textures[id];
		}

		public List<MTexture> GetAtlasSubtextures(string key)
		{
			List<MTexture> list;

			if (!orderedTexturesCache.TryGetValue(key, out list))
			{
				list = new List<MTexture>();

				var index = 0;
				while (true)
				{
					var texture = GetAtlasSubtextureFromAtlasAt(key, index);
					if (texture != null)
						list.Add(texture);
					else
						break;
					index++;
				}

				orderedTexturesCache.Add(key, list);
			}

			return list;
		}

		private MTexture GetAtlasSubtextureFromCacheAt(string key, int index)
		{
			return orderedTexturesCache[key][index];
		}

		private MTexture GetAtlasSubtextureFromAtlasAt(string key, int index)
		{
			if (index == 0 && textures.ContainsKey(key))
				return textures[key];

			var indexString = index.ToString();
			var startLength = indexString.Length;
			while (indexString.Length < startLength + 6)
			{
				MTexture result;
				if (textures.TryGetValue(key + indexString, out result))
					return result;
				indexString = "0" + indexString;
			}

			return null;
		}

		public MTexture GetAtlasSubtexturesAt(string key, int index)
		{
			List<MTexture> list;
			if (orderedTexturesCache.TryGetValue(key, out list))
				return list[index];
			else
				return GetAtlasSubtextureFromAtlasAt(key, index);
		}

		public void Dispose()
		{
			foreach (var texture in textures.Values)
				texture.Dispose();
			textures.Clear();
			orderedTexturesCache.Clear();
		}
	

	}
}
