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

namespace Monocle {

	public class LoadedAsset {
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
		public readonly bool IsModContent;
		public readonly long LastEdit;
		public readonly ContentLocationType AssetType;

		internal LoadedAsset(ZipArchiveEntry zip, string exactPath, DateTime lastEdit) {
			zipEntry = zip;

			AssetType = ContentLocationType.ZipFile;
			IsModContent = true;

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(zip.FullName);
			LiteralPath = exactPath;
			Path = zip.FullName;
		}
		internal LoadedAsset(string exactPath, string contentPath, DateTime lastEdit) {

			LastEdit = lastEdit.Ticks;

			Extention = System.IO.Path.GetExtension(contentPath);
			LiteralPath = exactPath;
			Path = contentPath;
			AssetType = ContentLocationType.Folder;

			if (System.IO.Path.GetFileName(LiteralPath.Replace("\\" + Path, "")) != "Content") {
				IsModContent = true;
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
	}
	public struct LevelPackHeader {
		public string PackName;
		public string PackNameSafe;
		public string LevelToStart;
	}

	public static class AssetLoader {
		//static AssetLoader() {
		//	yamlParse = new DeserializerBuilder().IncludeNonPublicProperties().WithNamingConvention(NullNamingConvention.Instance).Build();
		//	yamlSaver = new SerializerBuilder().IncludeNonPublicProperties().WithNamingConvention(NullNamingConvention.Instance).Build();
		//}

		//private readonly static IDeserializer yamlParse;
		//private readonly static ISerializer yamlSaver;

		public static Dictionary<string, LevelPackHeader> LevelPacks = new Dictionary<string, LevelPackHeader>();

		private static Dictionary<string, List<Action<string>>>
			assetAddedEvents = new Dictionary<string, List<Action<string>>>(),
			assetDeletedEvents = new Dictionary<string, List<Action<string>>>(),
			assetUpdatedEvents = new Dictionary<string, List<Action<string>>>();

		//public static T ParseYaml<T>(string yamlData) {
		//	return yamlParse.Deserialize<T>(yamlData);
		//}
		//public static string SerializeYaml(object yamlObject) {
		//	return yamlSaver.Serialize(yamlObject);
		//}

		private static FileSystemWatcher contentUpdateWatcher;

		private static Dictionary<string, LoadedAsset> content = new Dictionary<string, LoadedAsset>();
		private static List<ZipArchive> zipFiles = new List<ZipArchive>();

		public static bool HasContent(string path) {
			path = path.Replace('/', '\\');
			return content.ContainsKey(path);
		}
		public static LoadedAsset GetContent(string path) {
			path = path.Replace('/', '\\');

			if (content.ContainsKey(path)) {
				return content[path];
			}

			return null;
		}
		public static IEnumerable<LoadedAsset> FindAssetsByRegex(string pattern) {
			foreach (var key in content.Keys) {
				if (Regex.IsMatch(key, pattern)) {
					yield return content[key];
				}
			}
		}
		public static IEnumerable<LoadedAsset> FindAssetsByExtension(string ext) {
			foreach (var key in content.Keys) {
				if (Path.GetExtension(key) == ext) {
					yield return content[key];
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
		public static string GetText(string path) {
			return GetContent(path).GetText();
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

			foreach (var pair in content) {
				if (pair.Key.StartsWith(path) && (extention == null || pair.Value.Extention == extention))
					yield return pair.Value;
			}
		}

		public static void AddFolder(string path) {

			if (!Directory.Exists(path))
				return;


			void AddOpenContent(string path, string name) {
				if (Path.GetExtension(path) == ".xnb")
					return;

				if (content.ContainsKey(name))
					return;

				content[name] = new LoadedAsset(path, name, File.GetLastWriteTime(path));
			}

			//#if DEBUG
			//			string projectPath = Path.GetFullPath(VanillaContent + "\\..\\..\\..\\..\\Content");
			//			foreach (var dir in Directory.EnumerateFiles(projectPath, "*", SearchOption.AllDirectories)) {
			//				if (dir.EndsWith("mgcb") || dir.EndsWith("ldtk") || dir.EndsWith("spritefont"))
			//					continue;

			//				string subpath = dir[(projectPath.Length + 1)..];
			//				if (subpath.StartsWith("bin\\") || subpath.StartsWith("obj\\")) {
			//					continue;
			//				}

			//				AddOpenContent(dir, subpath);

			//				Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(VanillaContent, subpath)));

			//				//File.Copy(dir, Path.Combine(VanillaContent, subpath), true);

			//			}
			//#endif

			foreach (var dir in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) {
				string subpath = dir[(path.Length + 1)..];

				AddOpenContent(dir, subpath);
			}

			//if (Directory.Exists(ModdedContent)) {

			//	foreach (var dir in Directory.EnumerateDirectories(ModdedContent)) {

			//		foreach (var c in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {

			//			string ext = Path.GetExtension(c);
			//			if (ext == ".ldtk")
			//				continue;

			//			string subpath = c[(dir.Length + 1)..];

			//			if (subpath.StartsWith("Levels") && content.ContainsKey(subpath))
			//				continue;

			//			AddOpenContent(c, subpath);
			//		}
			//	}
			//	foreach (var zip in Directory.EnumerateFiles(ModdedContent, "*.zip")) {
			//		var file = ZipFile.Open(zip, ZipArchiveMode.Read);

			//		zipFiles.Add(file);

			//		DateTime lastEdit = File.GetLastWriteTime(zip);


			//		foreach (var entry in file.Entries) {

			//			if (entry.FullName.StartsWith("Levels") && content.ContainsKey(entry.FullName))
			//				continue;
			//			if (entry.FullName.StartsWith("Levels") && entry.FullName.EndsWith(".ldtkl"))
			//				continue;

			//			AddZipContent(entry, zip, lastEdit);
			//		}
			//	}

			//	//foreach (var item in FindAssetsByRegex(".+\\.pack")) {
			//	//	var pack = JsonConvert.DeserializeObject<LevelPackHeader>(item.GetText());

			//	//	LevelPacks.Add(pack.PackNameSafe, pack);
			//	//}


			//	contentUpdateWatcher = new FileSystemWatcher(ModdedContent, "*");

			//	contentUpdateWatcher.IncludeSubdirectories = true;

			//	contentUpdateWatcher.Created += ContentUpdateWatcher_Created;
			//	contentUpdateWatcher.Deleted += ContentUpdateWatcher_Deleted;
			//	contentUpdateWatcher.Changed += ContentUpdateWatcher_Changed;
			//	contentUpdateWatcher.Renamed += ContentUpdateWatcher_Renamed;

			//	contentUpdateWatcher.EnableRaisingEvents = true;
			//}
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
					content[zipEntry.FullName] = new LoadedAsset(zipEntry, zipPath, lastEdit);
				}
			}


			foreach (var entry in file.Entries) {

				if (entry.FullName.StartsWith("Levels") && content.ContainsKey(entry.FullName))
					continue;
				if (entry.FullName.StartsWith("Levels") && entry.FullName.EndsWith(".ldtkl"))
					continue;

				AddZipContent(entry, path, lastEdit);
			}
		}
		internal static void Initialize() {

			content.Clear();
			LevelPacks.Clear();

			AddFolder(Path.Combine(Directory.GetCurrentDirectory(), "Content"));
		}

		//private static void ContentUpdateWatcher_Renamed(object sender, RenamedEventArgs e) {
		//	ContentUpdateWatcher_Deleted(sender,
		//		new FileSystemEventArgs(WatcherChangeTypes.Renamed, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.OldFullPath)));
		//	ContentUpdateWatcher_Created(sender,
		//		new FileSystemEventArgs(WatcherChangeTypes.Renamed, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.FullPath)));
		//}

		//private static void ContentUpdateWatcher_Changed(object sender, FileSystemEventArgs e) {
		//	var path = e.FullPath.Replace(ModdedContent + '\\', "");
		//	var modName = path.Split('\\')[0];

		//	path = path.Remove(0, modName.Length);
		//	var localAsset = path.Substring(1);

		//	foreach (var l in assetUpdatedEvents) {
		//		if (path.StartsWith(l.Key)) {
		//			foreach (var item in l.Value) {
		//				item(localAsset);
		//			}
		//		}
		//	}
		//}

		//private static void ContentUpdateWatcher_Deleted(object sender, FileSystemEventArgs e) {
		//	var path = e.FullPath.Replace(ModdedContent + '\\', "");
		//}

		//private static void ContentUpdateWatcher_Created(object sender, FileSystemEventArgs e) {
		//	var path = e.FullPath.Replace(ModdedContent + '\\', "");
		//}

		internal static void Unload() {
			foreach (var zip in zipFiles) {
				zip.Dispose();
			}
			zipFiles.Clear();
			content.Clear();
		}

		public static void OnAssetAddedEvent(string subfolder, Action<string> onAdded) {
			subfolder = subfolder.Replace('/', '\\');
			if (!subfolder.EndsWith('\\'))
				subfolder += '\\';

			if (!assetAddedEvents.ContainsKey(subfolder)) {
				assetAddedEvents.Add(subfolder, new List<Action<string>>());
			}
			var item = assetAddedEvents[subfolder];
			item.Add(onAdded);
		}
		public static void OnAssetDeletedEvent(string subfolder, Action<string> onDeleted) {
			subfolder = subfolder.Replace('/', '\\');
			if (!subfolder.EndsWith('\\'))
				subfolder += '\\';


			if (!assetDeletedEvents.ContainsKey(subfolder)) {
				assetDeletedEvents.Add(subfolder, new List<Action<string>>());
			}
			var item = assetDeletedEvents[subfolder];
			item.Add(onDeleted);
		}
		public static void OnAssetUpdatedEvent(string subfolder, Action<string> onUpdated) {
			subfolder = subfolder.Replace('/', '\\');
			if (!subfolder.EndsWith('\\'))
				subfolder += '\\';


			if (!assetUpdatedEvents.ContainsKey(subfolder)) {
				assetUpdatedEvents.Add(subfolder, new List<Action<string>>());
			}
			var item = assetUpdatedEvents[subfolder];
			item.Add(onUpdated);
		}

	}
}
