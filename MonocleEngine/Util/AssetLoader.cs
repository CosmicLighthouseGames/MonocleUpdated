#if !CONSOLE
#define ALLOW_MODS
#endif

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;
using Monocle;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections;
using YamlDotNet.Serialization;
using System.Linq;

namespace Monocle {

	public abstract class MultiObjectAtlas<T> : IDictionary<string, T> {
		public class PriorityValue {
			public T Value;
			public int Priority;
			bool enabled = true;
			public bool Enabled {
				get => enabled;
				set {
					if (enabled != value) {
						enabled = value;
						parent.Sort(key);
					}

				}
			}
			readonly MultiObjectAtlas<T> parent;
			readonly string key;

			public PriorityValue(MultiObjectAtlas<T> parent, string key) {
				this.parent = parent;
				this.key = key;
			}
		}

		protected Dictionary<string, List<PriorityValue>> data = new Dictionary<string, List<PriorityValue>>();

		T IDictionary<string, T>.this[string key] { get => data[key][0].Value; set => data[key][0].Value = value; }

		ICollection<string> IDictionary<string, T>.Keys => data.Keys;

		ICollection<T> IDictionary<string, T>.Values => data.Values.Select(a => a[0].Value).ToList();

		int ICollection<KeyValuePair<string, T>>.Count => data.Count;

		bool ICollection<KeyValuePair<string, T>>.IsReadOnly => false;


		void ICollection<KeyValuePair<string, T>>.Clear() {
			data.Clear();
		}
		public void Clear() {
			data.Clear();
		}

		bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item) {
			return false;
		}

		bool IDictionary<string, T>.ContainsKey(string key) {
			return ContainsKey(key);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return data.GetEnumerator();
		}

		bool IDictionary<string, T>.Remove(string key) {
			if (data.ContainsKey(key)) {
				data[key].Clear();
				return true;
			}
			return false;
		}

		bool IDictionary<string, T>.TryGetValue(string key, out T value) {
			return TryGetValue(key, out value);
		}
		public bool TryGetValue(string key, out T value) {
			value = default;
			if (!ContainsKey(key))
				return false;

			value = data[key][0].Value;
			return true;
		}

		public bool ContainsKey(string key) {
			return data.ContainsKey(key) && data[key].Count > 0;
		}

		public PriorityValue Add(string key, T value, int priority) {
			if (!data.ContainsKey(key)) {
				data[key] = new List<PriorityValue>();
			}
			var val = new PriorityValue(this, key) { Priority = priority, Value = value };

			var list = data[key];
			list.Add(val);

			Sort(key);

			return val;
		}

		public PriorityValue GetHighestValue(string key) {
			return data[key][0];
		}

		void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int arrayIndex) {
			foreach (var item in data) {
				array[arrayIndex++] = new KeyValuePair<string, T>(item.Key, item.Value[0].Value);
			}
		}

		IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator() {
			foreach (var item in data) {
				yield return new KeyValuePair<string, T>(item.Key, item.Value[0].Value);
			}
		}
		void IDictionary<string, T>.Add(string key, T value) {

		}

		void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item) {

		}
		bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item) {
			return false;
		}

		void Sort(string key) {

			var list = data[key];
			if (list.Count == 1) {
				return;
			}

			var first = list[0];
			list.Sort((a, b) => a.Enabled != b.Enabled ? b.Enabled.CompareTo(a.Enabled) : b.Priority.CompareTo(a.Priority));

			if (first != list[0]) {
				OnUpdated(key, list[0]);
			}
		}

		protected virtual void OnUpdated(string key, PriorityValue value) {

		}
	}

	public partial class PackMetadata {

		[YamlIgnore]
		public string LiteralPath { get; internal set; }

		public bool IsAssetPack;
		public int Priority;

		internal void Copy(PackMetadata other) {
			foreach (var field in typeof(PackMetadata).GetFields(BindingFlags.Public | BindingFlags.Instance)) {
				if (Attribute.IsDefined(field, typeof(YamlIgnoreAttribute)))
					continue;
				field.SetValue(this, field.GetValue(other));
			}
		}
	}
	public class LoadedAsset {

		static IEnumerable<KeyValuePair<string, string>> GetMeta(string[] data) {

			foreach (var l in data) {
				if (!l.Contains(':'))
					continue;
				string[] split = new string[]{
					l.Substring(0, l.IndexOf(':')),
					l.Substring(l.IndexOf(":") + 1)
				};
				if (split.Length > 0) {
					yield return new KeyValuePair<string, string>(split[0], split[1]);
				}
			}

			yield break;

		}
		public enum ContentType {
			Art,
			Effect,
			Level,
			Meta,
			LevelPart,
		}
		public enum ContentLocationType {
			Folder,
			ZipFile,
		}

		public Stream ContentStream {
			get {
				if (zipEntry != null)
					return zipEntry.Open();
				else {
					for (int i = 0; i < 10; i++) {
						try {

							return new FileStream(LiteralPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						}
						catch {
							Thread.Sleep(i * i);
						}
					}
					throw new FileLoadException();
				}
			}
		}
		
		private ZipArchiveEntry zipEntry;
		public readonly string Extention;
		public readonly string LiteralPath;
		public readonly string Path;
		public readonly long LastEdit;
		public readonly ContentLocationType AssetType;
		public readonly Dictionary<string, string> ItemMetaData;
		public readonly PackMetadata PackMetaData;

		internal LoadedAsset(PackMetadata meta, ZipArchiveEntry zip, string exactPath, DateTime lastEdit) {
			PackMetaData = meta;
			zipEntry = zip;

			AssetType = ContentLocationType.ZipFile;

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(zip.FullName);
			LiteralPath = exactPath;
			Path = zip.FullName;

			ItemMetaData = new Dictionary<string, string>();
		}
		internal LoadedAsset(PackMetadata meta, string exactPath, string contentPath, DateTime lastEdit) {
			PackMetaData = meta;

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(contentPath);
			LiteralPath = exactPath;
			Path = contentPath;
			AssetType = ContentLocationType.Folder;

			ItemMetaData = new Dictionary<string, string>();
			
			while (!string.IsNullOrWhiteSpace(contentPath)) {
				if (File.Exists(exactPath + ".meta")) {
					foreach (var pair in GetMeta(File.ReadAllLines(exactPath + ".meta"))) {
						if (!ItemMetaData.ContainsKey(pair.Key)) {
							ItemMetaData[pair.Key] = pair.Value;
						}
					}
				}
				exactPath = System.IO.Path.GetDirectoryName(exactPath);
				contentPath = System.IO.Path.GetDirectoryName(contentPath);
			}
		}

		public string GetText() {

			string retval;
			using (var reader = new StreamReader(ContentStream)) {
				retval = reader.ReadToEnd();
			}
			return retval;
		}

		public byte[] GetBinary() {

			byte[] retval;
			using (var reader = new BinaryReader(ContentStream)) {
				retval = reader.ReadBytes((int)reader.BaseStream.Length);
			}
			return retval;
		}

		public bool IsInFolder(string folder) {
			return Path.Replace('/', '\\').StartsWith(folder.Replace('/', '\\'));
		}

		public ZipArchive GetZipContent() {

			return new ZipArchive(ContentStream, ZipArchiveMode.Read);
		}
	}

	public static class AssetLoader {

		public static IDeserializer metaDeserializer;
		public static ISerializer metaSerializer;


		public static void PauseRefreshing() {
			pausedListening = true;
		}
		public static void ResumeRefreshing() {
			pausedListening = false;
		}
		static bool pausedListening;

		class FolderWatcher {
			internal string Directory;
			private FileSystemWatcher contentUpdateWatcher;
			private PackMetadata metadata;

			public FolderWatcher(string dir, PackMetadata metadata) {
				Directory = dir;
				this.metadata = metadata;

				contentUpdateWatcher = new FileSystemWatcher(dir, "*");

				contentUpdateWatcher.IncludeSubdirectories = true;

				contentUpdateWatcher.Created += Created;
				contentUpdateWatcher.Deleted += Deleted;
				contentUpdateWatcher.Changed += Changed;
				contentUpdateWatcher.Renamed += Renamed;

				contentUpdateWatcher.EnableRaisingEvents = true;
			}

			public event Action<string, string, PackMetadata> OnAssetAdded, OnAssetUpdated, OnAssetDeleted;


			private void Renamed(object sender, RenamedEventArgs e) {
				Deleted(sender,
					new FileSystemEventArgs(WatcherChangeTypes.Renamed, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.OldFullPath)));
				Created(sender,
					new FileSystemEventArgs(WatcherChangeTypes.Renamed, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.FullPath)));
			}

			private void Changed(object sender, FileSystemEventArgs e) {
				var path = e.FullPath.Replace(Directory + '\\', "");

				var ext = Path.GetExtension(path);
				if (string.IsNullOrWhiteSpace(ext) || path.Contains('~')) {
					return;
				}

				while (pausedListening || !File.Exists(e.FullPath)) {
					Thread.Sleep(1);
				}
				while (File.Exists(e.FullPath)) {
					try {
						File.OpenRead(e.FullPath).Close();

						break;
					}
					catch {

					}
				}

				pausedListening = true;
				try {

					OnAssetUpdated?.Invoke(path, e.FullPath, metadata);
				}
				finally {

					pausedListening = false;
				}
			}

			private void Deleted(object sender, FileSystemEventArgs e) {
				var path = e.FullPath.Replace(Directory + '\\', "");

				var ext = Path.GetExtension(path);
				if (string.IsNullOrWhiteSpace(ext) || path.Contains('~')) {
					return;
				}

				while (pausedListening) {
					Thread.Sleep(1);
				}

				pausedListening = true;
				OnAssetDeleted?.Invoke(path, e.FullPath, metadata);
				pausedListening = false;
			}

			private void Created(object sender, FileSystemEventArgs e) {
				var path = e.FullPath.Replace(Directory + '\\', "");

				var ext = Path.GetExtension(path);
				if (string.IsNullOrWhiteSpace(ext) || path.Contains('~')) {
					return;
				}

				while (pausedListening) {
					Thread.Sleep(1);
				}
				while (true) {
					try {
						File.OpenRead(e.FullPath).Close();

						break;
					}
					catch {

					}
				}

				pausedListening = true;
				OnAssetAdded?.Invoke(path, e.FullPath, metadata);
				pausedListening = false;
			}

		}
		static AssetLoader() {
			metaDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
			metaSerializer = new SerializerBuilder().Build();
		}

		//private readonly static IDeserializer yamlParse;
		//private readonly static ISerializer yamlSaver;

		public static event Action<LoadedAsset> OnAssetAdded, OnAssetUpdated, OnAssetDeleted;

		static List<FolderWatcher> Folders = new List<FolderWatcher>();

		//public static T ParseYaml<T>(string yamlData) {
		//	return yamlParse.Deserialize<T>(yamlData);
		//}
		//public static string SerializeYaml(object yamlObject) {
		//	return yamlSaver.Serialize(yamlObject);
		//}


		private static Dictionary<string, List<LoadedAsset>> Content = new Dictionary<string, List<LoadedAsset>>();
		private static Dictionary<string, ZipArchive> zipFiles = new Dictionary<string, ZipArchive>();
		private static Dictionary<string, PackMetadata> metaDatas = new Dictionary<string, PackMetadata>();

		public static bool HasContent(string path) {
			path = path.Replace('/', '\\');
			return Content.ContainsKey(path) && Content[path].Count > 0;
		}
		public static LoadedAsset GetContent(string path) {
			path = path.Replace('/', '\\');

			if (Content.ContainsKey(path) && Content[path].Count > 0) {
				return Content[path][0];
			}

			return null;
		}
		public static ZipArchive GetZipContent(string path) {

			path = path.Replace('/', '\\');

			if (Content.ContainsKey(path) && Content[path].Count > 0) {
				return new ZipArchive(Content[path][0].ContentStream, ZipArchiveMode.Read);
			}

			return null;
		}
		public static LoadedAsset[] GetContents(string path) {
			path = path.Replace('/', '\\');

			if (Content.ContainsKey(path)) {
				return Content[path].ToArray();
			}

			return new LoadedAsset[0];
		}
		public static IEnumerable<LoadedAsset> FindAssetsByRegex(string pattern) {
			foreach (var key in Content.Keys) {
				if (Regex.IsMatch(key, pattern)) {
					foreach (var c in Content[key]) {
						yield return c;
					}
				}
			}
		}
		public static IEnumerable<LoadedAsset> FindAssetsByExtension(string ext) {
			foreach (var key in Content.Keys) {
				if (Path.GetExtension(key) == ext) {
					foreach (var c in Content[key]) {
						yield return c;
					}
				}
			}
		}
		public static BinaryFile GetBin(string path) {
			BinaryFile retval;
			using (var stream = GetContent(path + ".bin").ContentStream) {
				retval = new BinaryFile(stream);
			}

			return retval;
		}
		[DebuggerHidden]
		public static string GetText(string path) {
			return GetContent(path).GetText();
		}
		[DebuggerHidden]
		public static T GetJson<T>(string path) {
			return JsonConvert.DeserializeObject<T>(GetContent(path).GetText());
		}

		public static string GetLiteralPath(string assetPath) {

			var asset = GetContent(assetPath);

			if (asset.AssetType != LoadedAsset.ContentLocationType.ZipFile)
				return asset.LiteralPath;
			else
				return null;
		}

		public static IEnumerable<LoadedAsset> GetContentInFolder(string path, string extention = null) {
			path = path.Replace('/', '\\');
			if (!path.EndsWith('\\'))
				path += '\\';

			var list = Content.Keys.OrderBy(a => a).ToList();

			foreach (var pair in Content) {
				if (pair.Key.StartsWith(path) && (extention == null || pair.Value[0].Extention == extention)) {
					foreach (var c in pair.Value) {
						yield return c;
					}
				}
			}
		}

		static LoadedAsset AddOpenContent(string path, string name, PackMetadata metadata) {
			if (Path.GetExtension(path) == ".xnb")
				return null;

			path = path.Replace('/', '\\');
			name = name.Replace('/', '\\');

			var asset = new LoadedAsset(metadata, path, name, File.GetLastWriteTime(path));

			if (!Content.ContainsKey(name)) {
				Content[name] = new List<LoadedAsset>();
			}
			Content[name].Add(asset);

			return asset;
		}

		public static PackMetadata AddFolder(string path) {

			if (!Directory.Exists(path))
				return null;

			if (metaDatas.ContainsKey(path))
				return metaDatas[path];

			PackMetadata metadata = new PackMetadata();

			foreach (var dir in Directory.EnumerateFiles(path, "*.metadata.yaml")) {

				metadata = metaDeserializer.Deserialize<PackMetadata>(File.ReadAllText(dir));
				metadata.LiteralPath = dir;

				break;
			}
			metaDatas[path] = metadata;


			foreach (var dir in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) {

				string subpath = dir[(path.Length + 1)..];

				if (subpath.EndsWith(".metadata.yaml"))
					continue;

				var item = AddOpenContent(dir, subpath, metadata);
			}

			var watcher = new FolderWatcher(path, metadata);
			watcher.OnAssetAdded += OnAdded;
			watcher.OnAssetDeleted += OnDeleted;
			watcher.OnAssetUpdated += OnChanged;

			Folders.Add(watcher);

			return metadata;

		}
		public static PackMetadata AddZipFile(string path) {

			if (metaDatas.ContainsKey(path))
				return metaDatas[path];

			var meta = new PackMetadata();

			var file = ZipFile.Open(path, ZipArchiveMode.Read);

			foreach (var entry in file.Entries) {
				if (Regex.IsMatch(entry.FullName, "\\w+.metadata.yaml")) {
					using StreamReader s = new StreamReader(entry.Open());
					meta = metaDeserializer.Deserialize<PackMetadata>(s.ReadToEnd());

					meta.LiteralPath = path;
					break;
				}
			}

			zipFiles.Add(path, file);

			DateTime lastEdit = File.GetLastWriteTime(path);

			void AddZipContent(ZipArchiveEntry zipEntry, string zipPath, DateTime lastEdit) {
				if (Path.GetExtension(zipPath) == ".ldtkl" || zipPath.EndsWith(".metadata.yaml")) {
					// Ignore raw level files.
				}
				else {
					var asset = new LoadedAsset(meta, zipEntry, zipPath, lastEdit);
					if (!Content.ContainsKey(zipEntry.FullName)) {
						Content[zipEntry.FullName] = new List<LoadedAsset>();
					}
					Content[zipEntry.FullName].Add(asset);

				}
			}




			foreach (var entry in file.Entries) {

				if (entry.FullName.StartsWith("Levels") && Content.ContainsKey(entry.FullName))
					continue;
				if (entry.FullName.StartsWith("Levels") && entry.FullName.EndsWith(".ldtkl"))
					continue;

				AddZipContent(entry, path, lastEdit);
			}

			return meta;
		}
		public static void RemoveZipFile(string path) {

			zipFiles.Remove(path);

			foreach (var content in Content.Values) {
				foreach (var asset in content) {
					if (asset.LiteralPath == path) {
						content.Remove(asset);
						break;
					}
				}
			}
		}
		internal static void Initialize() {

			//Content.Clear();

			AddFolder(Path.Combine(Directory.GetCurrentDirectory(), "Content"));
		}

		private static void OnChanged(string path, string fullPath, PackMetadata metadata) {

			if (path.EndsWith(".metadata.yaml")) {
				string dir = Path.GetDirectoryName(fullPath);

				if (metaDatas.ContainsKey(dir)) {
					metaDatas[dir].Copy(metaDeserializer.Deserialize<PackMetadata>(File.ReadAllText(fullPath)));
				}
			}
			else {
				if (!Content.ContainsKey(path)) {
					return;
				}
				foreach (var c in Content[path]) {
					if (c.LiteralPath == fullPath) {
						OnAssetUpdated?.Invoke(c);
						break;
					}
				}
			}
		}

		private static void OnDeleted(string path, string fullPath, PackMetadata metadata) {

			if (!Content.ContainsKey(path)) {
				return;
			}
			foreach (var c in Content[path]) {
				if (c.LiteralPath == fullPath) {
					OnAssetDeleted?.Invoke(c);
					Content[path].Remove(c);
					break;
				}
			}
		}

		private static void OnAdded(string path, string fullPath, PackMetadata metadata) {
			if (path.EndsWith(".metadata.yaml")) {
				string dir = Path.GetDirectoryName(fullPath);
				
				if (metaDatas.ContainsKey(dir)) {
					metaDatas[dir].Copy(metaDeserializer.Deserialize<PackMetadata>(File.ReadAllText(fullPath)));
				}
			}
			else {

				OnAssetAdded?.Invoke(AddOpenContent(fullPath, path, metadata));
			}

		}

		internal static void Unload() {
			foreach (var zip in zipFiles) {
				zip.Value.Dispose();
			}
			zipFiles.Clear();
			Content.Clear();
		}


		public static IEnumerable<PackMetadata> GetMetadatas() {
			return metaDatas.Values;
		}
	}
}
