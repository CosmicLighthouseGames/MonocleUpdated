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

namespace Monocle {

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
		public readonly Dictionary<string, string> MetaData;

		internal LoadedAsset(ZipArchiveEntry zip, string exactPath, DateTime lastEdit) {
			zipEntry = zip;

			AssetType = ContentLocationType.ZipFile;

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(zip.FullName);
			LiteralPath = exactPath;
			Path = zip.FullName;

			MetaData = new Dictionary<string, string>();
		}
		internal LoadedAsset(string exactPath, string contentPath, DateTime lastEdit) {

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(contentPath);
			LiteralPath = exactPath;
			Path = contentPath;
			AssetType = ContentLocationType.Folder;

			MetaData = new Dictionary<string, string>();
			
			while (!string.IsNullOrWhiteSpace(contentPath)) {
				if (File.Exists(exactPath + ".meta")) {
					foreach (var pair in GetMeta(File.ReadAllLines(exactPath + ".meta"))) {
						if (!MetaData.ContainsKey(pair.Key)) {
							MetaData[pair.Key] = pair.Value;
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
	}

	public static class AssetLoader {

		class FolderWatcher {
			internal string Directory;
			private FileSystemWatcher contentUpdateWatcher;

			public FolderWatcher(string dir) {
				Directory = dir;

				contentUpdateWatcher = new FileSystemWatcher(dir, "*");

				contentUpdateWatcher.IncludeSubdirectories = true;

				contentUpdateWatcher.Created += Created;
				contentUpdateWatcher.Deleted += Deleted;
				contentUpdateWatcher.Changed += Changed;
				contentUpdateWatcher.Renamed += Renamed;

				contentUpdateWatcher.EnableRaisingEvents = true;
			}

			public event Action<string, string> OnAssetAdded, OnAssetUpdated, OnAssetDeleted;


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

				OnAssetUpdated?.Invoke(path, e.FullPath);
			}

			private void Deleted(object sender, FileSystemEventArgs e) {
				var path = e.FullPath.Replace(Directory + '\\', "");

				var ext = Path.GetExtension(path);
				if (string.IsNullOrWhiteSpace(ext) || path.Contains('~')) {
					return;
				}

				OnAssetDeleted?.Invoke(path, e.FullPath);
			}

			private void Created(object sender, FileSystemEventArgs e) {
				var path = e.FullPath.Replace(Directory + '\\', "");

				var ext = Path.GetExtension(path);
				if (string.IsNullOrWhiteSpace(ext) || path.Contains('~')) {
					return;
				}

				OnAssetAdded?.Invoke(path, e.FullPath);
			}

		}
		//static AssetLoader() {
		//	yamlParse = new DeserializerBuilder().IncludeNonPublicProperties().WithNamingConvention(NullNamingConvention.Instance).Build();
		//	yamlSaver = new SerializerBuilder().IncludeNonPublicProperties().WithNamingConvention(NullNamingConvention.Instance).Build();
		//}

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
		private static List<ZipArchive> zipFiles = new List<ZipArchive>();

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
					//yield return content[key];
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

			foreach (var pair in Content) {
				if (pair.Key.StartsWith(path) && (extention == null || pair.Value[0].Extention == extention)) {
					foreach (var c in pair.Value) {
						yield return c;
					}
				}
			}
		}

		static LoadedAsset AddOpenContent(string path, string name) {
			if (Path.GetExtension(path) == ".xnb")
				return null;

			path = path.Replace('/', '\\');
			name = name.Replace('/', '\\');

			var asset = new LoadedAsset(path, name, File.GetLastWriteTime(path));

			if (!Content.ContainsKey(name)) {
				Content[name] = new List<LoadedAsset>();
			}
			Content[name].Add(asset);

			return asset;
		}

		public static void AddFolder(string path) {

			if (!Directory.Exists(path))
				return;

			foreach (var folder in Folders) {
				if (folder.Directory == path)
					return;
			}

			foreach (var dir in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) {
				string subpath = dir[(path.Length + 1)..];

				AddOpenContent(dir, subpath);
			}

			var watcher = new FolderWatcher(path);
			watcher.OnAssetAdded += OnAdded;
			watcher.OnAssetDeleted += OnDeleted;
			watcher.OnAssetUpdated += OnChanged;

			Folders.Add(watcher);

		}
		public static void AddZipFile(string path) {

			var file = ZipFile.Open(path, ZipArchiveMode.Read);

			zipFiles.Add(file);

			DateTime lastEdit = File.GetLastWriteTime(path);

			void AddZipContent(ZipArchiveEntry zipEntry, string zipPath, DateTime lastEdit) {
				if (zipEntry.FullName.StartsWith("Lang")) { // Load language content differently
															//var open = new StreamReader(zipEntry.Open());
															//LanguageControl.LoadLanguage(Path.GetFileNameWithoutExtension(zipEntry.FullName), ParseYaml<Dictionary<string, string>>(open.ReadToEnd()));
				}
				else if (Path.GetExtension(zipPath) == ".ldtkl") {
					// I don't know if I should allow raw levels
				}
				else {
					var asset = new LoadedAsset(zipEntry, zipPath, lastEdit);
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
		}
		internal static void Initialize() {

			Content.Clear();

			AddFolder(Path.Combine(Directory.GetCurrentDirectory(), "Content"));
		}

		private static void OnChanged(string path, string fullPath) {

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

		private static void OnDeleted(string path, string fullPath) {

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

		private static void OnAdded(string path, string fullPath) {
			OnAssetAdded?.Invoke(AddOpenContent(fullPath, path));
		}

		internal static void Unload() {
			foreach (var zip in zipFiles) {
				zip.Dispose();
			}
			zipFiles.Clear();
			Content.Clear();
		}

	}
}
