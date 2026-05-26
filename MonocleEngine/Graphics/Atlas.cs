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
using System.Threading;

namespace Monocle
{
	public class Atlas : MultiObjectAtlas<Texture2D> {
		private Dictionary<string, MTexture> textures = new Dictionary<string, MTexture>();
		private Dictionary<LoadedAsset, PriorityValue> itemToSprite = new Dictionary<LoadedAsset, PriorityValue>();
		private Dictionary<string, List<MTexture>> orderedTexturesCache = new Dictionary<string, List<MTexture>>();
		private Dictionary<PackMetadata, List<PriorityValue>> packs = new Dictionary<PackMetadata, List<PriorityValue>>();



		public MTexture this[string key] { get => textures[key]; }

		string Folder;

		public Atlas() {
			AssetLoader.OnAssetAdded += OnAdded;
			AssetLoader.OnAssetUpdated += OnUpdated;
		}

		private void OnUpdated(LoadedAsset item) {
			OnAdded(item);
		}

		protected override void OnUpdated(string key, PriorityValue value) {
			textures[key].SetTexture(value.Value);
		}

		private void Add(string path, Texture2D tex, LoadedAsset asset) {
			if (!textures.ContainsKey(path))
				textures[path] = new MTexture(tex);

			if (!packs.ContainsKey(asset.PackMetaData))
				packs.Add(asset.PackMetaData, new List<PriorityValue>());

			itemToSprite[asset] = Add(path, tex, asset.PackMetaData.IsAssetPack ? asset.PackMetaData.Priority : int.MinValue);
			packs[asset.PackMetaData].Add(itemToSprite[asset]);
		}
		private void Set(string key, Texture2D tex, LoadedAsset item) {
			if (itemToSprite.ContainsKey(item)) {

				var path = itemToSprite[item];

				path.Value = tex;
				if (ContainsKey(key) && GetHighestValue(key) == path) {
					textures[key].SetTexture(tex);
				}
				else {
					Add(key, tex, item);
				}
			}
			else {
				Add(key, tex, item);
			}
		}

		private void OnAdded(LoadedAsset item) {
			if (item.IsInFolder(Folder)) {

				Engine.OnNextFrame += () => {

					for (int i = 0; i < 10; i++) {


						string filepath = Path.ChangeExtension(item.Path, null);
						filepath = filepath.Replace('\\', '/');
						filepath = filepath.Substring(Folder.Length + 1);

						Texture2D tex = null;

						try {
							tex = Texture2D.FromStream(Draw.GraphicsDevice, item.ContentStream);
							

							if (OnLoadTexture != null) {
								tex = OnLoadTexture(item, tex);
							}

						}
						catch {
							Thread.Sleep(10);
						}

						if (tex == null)
							return;

						Set(filepath, tex, item);

						return;
					}
				};

			}
		}

		public event Func<LoadedAsset, Texture2D, Texture2D> OnLoadTexture;

		public static Atlas FromAssetLoader(string contentFolder, Func<LoadedAsset, Texture2D, Texture2D> loadTex = null) {

			contentFolder = contentFolder.Replace('\\', '/');

			var atlas = new Atlas();
			atlas.Folder = contentFolder;

			var graphics = Engine.Instance.GraphicsDevice;

			if (AssetLoader.HasContent($"{contentFolder}.bin")) {

				foreach (var con in AssetLoader.GetContents($"{contentFolder}.bin")) {

					var content = con.GetZipContent();

					foreach (var entry in content.Entries) {

						Texture2D texture;
						string filepath;

						switch (Path.GetExtension(entry.FullName)) {
							case ".png":

								// make nice for dictionary
								filepath = Path.ChangeExtension(entry.FullName, null);
								filepath = filepath.Replace('\\', '/');

								texture = Texture2D.FromStream(graphics, entry.Open());

							break;
							default:
								continue;
						}
						if (texture == null)
							continue;

						// load

						atlas.Set(filepath, texture, con);
					}
				}

			}

			foreach (var item in AssetLoader.GetContentInFolder(contentFolder)) {

				Texture2D texture;
				string filepath;

				switch (item.Extention) {
					case ".png":
					case ".xnb": {
						

						// make nice for dictionary
						filepath = Path.ChangeExtension(item.Path, null);
						filepath = filepath.Replace('\\', '/');
						filepath = filepath.Substring(contentFolder.Length + 1);

						texture = Texture2D.FromStream(graphics, item.ContentStream);
						
					}
						break;
					default:
						continue;
				}
				if (texture == null)
					continue;

				if (loadTex != null) {
					texture = loadTex(item, texture);
				}
				// load

				atlas.Set(filepath, texture, item);

			}

			return atlas;
		}

		public void SetPackEnabled(PackMetadata metadata, bool value) {
			if (!packs.ContainsKey(metadata))
				return;
			foreach (var item in packs[metadata]) {
				item.Enabled = value;
			}
		}


		public bool Has(string id)
		{
			return ContainsKey(id);
		}

		public MTexture GetOrDefault(string id, MTexture defaultTexture)
		{
			if (String.IsNullOrEmpty(id) || !Has(id))
				return defaultTexture;
			return this[id];
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
					else if (index > 0)
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
			if (index == 0 && ContainsKey(key))
				return this[key];

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
			foreach (var texture in this)
				texture.Value.Dispose();
			Clear();
			orderedTexturesCache.Clear();
		}
	
		
	}
}
