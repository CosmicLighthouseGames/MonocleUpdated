#pragma warning disable CA1416

using System.Reflection;
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Xml;
using System.Reflection.PortableExecutable;

namespace MonocleCompiler {
	
	public struct ImageMeta {
		public string type;
		public int byteAmount;
		public bool threeDimensions;
		public Dictionary<string, string> fullData;
	}

	public class CompilerBase {

		public static string EffectsCompiler;

		Dictionary<string, long> oldEditTimes = new Dictionary<string, long>(),
		currentEditTimes = new Dictionary<string, long>();

		Dictionary<string, byte[]> binaryData;

		string RawFilesPath, CompiledPath, DumpPath;

		public bool isDebug { get; private set; }

		string solutionPath, projectFolder, projectName, contentPath, engineContentPath, compiledPath;

		Dictionary<string, string> projectPaths;

		public List<string> IgnoredFolders = new List<string>();

		string[] args;

		[DebuggerHidden]
		public CompilerBase(string[] args) {
			string dir = Assembly.GetExecutingAssembly().Location;
			this.args = args;

			dir = Path.GetDirectoryName(dir)!;
			while (!string.IsNullOrWhiteSpace(dir) && solutionPath == null) {
				foreach (var path in Directory.GetFiles(dir)) {
					if (path.EndsWith(".sln")) {
						solutionPath = path;
						break;
					}
				}
				dir = Path.GetDirectoryName(dir)!;
			}

			SetValues();
		}

		private void SetValues() {

			projectPaths = new Dictionary<string, string>();
			isDebug = args[1].Contains("Debug");

			foreach (var line in File.ReadLines(solutionPath)) {
				var match = Regex.Match(line, @"Project\(""{.+?}""\) = ""(.+?)"", ""(.+?)"".+");
				if (match.Success) {
					projectPaths.Add(match.Groups[1].Value, match.Groups[2].Value);
				}
			}

			if (EffectsCompiler == null) {
				List<string> tocheck = new List<string>();

				string versionNeeded = null;

				foreach (var proj in projectPaths) { 
					if (proj.Value.EndsWith(".csproj")) {
						string folder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, proj.Value))!;
						string file = Path.Combine(folder, "obj", $"{proj.Key}.csproj.nuget.g.props");

						XmlDocument xmlDoc = new XmlDocument();
						xmlDoc.Load(file);
						var sec = xmlDoc["Project"]!["PropertyGroup"]!["NuGetPackageFolders"]!;
						string[] split = sec.InnerText.Split(';');
						foreach (string line in split) {
							if (!tocheck.Contains(line))
								tocheck.Add(line);
						}

						xmlDoc.Load(Path.Combine(Path.GetDirectoryName(solutionPath)!, proj.Value));
						sec = xmlDoc["Project"]!;

						foreach (XmlNode nodesA in sec.ChildNodes) {
							if (nodesA.Name == "ItemGroup") {
								foreach (XmlNode nodesB in nodesA.ChildNodes) {
									if (nodesB.Name == "PackageReference") {
										var include = nodesB.Attributes!["Include"]!;

										if (include.InnerText == "MonoGame.Content.Builder.Task")
											versionNeeded = nodesB.Attributes!["Version"]!.InnerText;
									}
								}
							}
						}
					}
				}

				if (versionNeeded == null)
					throw new Exception();

				foreach (var path in tocheck) {
					foreach (var dir in Directory.EnumerateDirectories(path)) {
						if (Path.GetFileName(dir) == "monogame.content.builder.task") {

							if (Directory.Exists(Path.Combine(dir, versionNeeded, "tools"))) {
								foreach (var file in Directory.EnumerateFiles(Path.Combine(dir, versionNeeded, "tools"), "*", SearchOption.AllDirectories)) {
									if (file.EndsWith("mgfxc.exe")) {
										EffectsCompiler = Path.GetDirectoryName(file)!;
										break;
									}
								}
							}
						}
					}
				}

				if (EffectsCompiler == null)
					throw new Exception();

			}

			projectFolder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths[args[0]]))!;
			contentPath = Path.Combine(projectFolder, "Content");

			if (projectPaths.ContainsKey("Monocle")) {
				engineContentPath = Path.Combine(Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths["Monocle"]))!, "Content");
			}
			else {
				engineContentPath = null;
			}
			compiledPath = Path.Combine(projectFolder, "obj", "Monocle");
		}

		public virtual ImageMeta DefaultImageMeta => 
			new ImageMeta() {
				type = "image",
				byteAmount = 1,
				threeDimensions = false,
				fullData = new Dictionary<string, string>() {
					{"type", "image" },
					{"byteAmount", "1" },
					{"threeDimensions", "false" },
				}
			};


		public void CompileSprites() {

			bool dirty = false;

			RawFilesPath = Path.Combine(contentPath, "Graphics");
			CompiledPath = Path.Combine(compiledPath, "graphics");
			DumpPath = Path.Combine(projectFolder, args[1], "Content", "Graphics");

			if (!Directory.Exists(DumpPath)) {
				Directory.CreateDirectory(DumpPath);
			}
			if (!Directory.Exists(CompiledPath)) {
				Directory.CreateDirectory(CompiledPath);
			}
			else if (File.Exists(Path.Combine(CompiledPath, "editTime.d"))) {
				try {
					using (var stream = new BinaryReader(File.Open(Path.Combine(CompiledPath, "editTime.d"), FileMode.Open))) {
#if !DEBUG
						while (stream.BaseStream.Position < stream.BaseStream.Length) {
							oldEditTimes.Add(stream.ReadString(), stream.ReadInt64());
						}
#endif
					}
				}
				catch {
					oldEditTimes.Clear();
				}
			}

			if (Directory.Exists(RawFilesPath)) {
				foreach (var file in Directory.EnumerateFiles(RawFilesPath, "*.png", SearchOption.AllDirectories)) {
					string localPath = file.Remove(0, RawFilesPath.Length + 1);
					long editTime = File.GetLastWriteTime(file).Ticks;
					currentEditTimes.Add(localPath, editTime);

					if (dirty || !oldEditTimes.ContainsKey(localPath) || oldEditTimes[localPath] != editTime)
						dirty = true;
				}

				if (dirty) {
					WriteEditTimes();

					binaryData = new Dictionary<string, byte[]>();

					ImageMeta meta = DefaultImageMeta;

					foreach (var dir in Directory.EnumerateDirectories(RawFilesPath)) {
						CompileSprites(dir, meta);
					}

					Dictionary<string, BinaryWriter> filePaths = new Dictionary<string, BinaryWriter>();



					foreach (var file in binaryData) {
						string pack = file.Key.Split('\\')[0];
						if (!filePaths.ContainsKey(pack)) {
							filePaths[pack] = new BinaryWriter(File.Open(Path.Combine(CompiledPath, pack + ".bin"), FileMode.Create));
						}
						var packFile = filePaths[pack];

						packFile.Write(file.Key.Remove(0, pack.Length + 1).Replace('\\', '/').Replace(".png", ""));
						packFile.Write(file.Value);
					}

					foreach (var file in filePaths.Values) {
						file.Close();
						file.Dispose();
					}

					// Go through each file in Graphics, get the edit times
					// if the edit times are exact, don't do anything
					// foreach file
					//		Check if it needs compiling.  

					CopyFiles();

				}
				else {
					CopyFiles();
				}
			}
			currentEditTimes.Clear();
			oldEditTimes.Clear();
		}
		//[DebuggerHidden]
		public void CompileEffects() {

			bool dirty = false;
			RawFilesPath = Path.Combine(contentPath, "Effects");
			CompiledPath = Path.Combine(compiledPath, "effects");
			DumpPath = Path.Combine(projectFolder, args[1], "Content", "Effects");

			if (!Directory.Exists(DumpPath)) {
				Directory.CreateDirectory(DumpPath);
			}
			if (!Directory.Exists(CompiledPath)) {
				Directory.CreateDirectory(CompiledPath);
			}
			else if (File.Exists(Path.Combine(CompiledPath, "editTime.d"))) {
				try {
					using (var stream = new BinaryReader(File.Open(Path.Combine(CompiledPath, "editTime.d"), FileMode.Open))) {
						while (stream.BaseStream.Position < stream.BaseStream.Length) {
							oldEditTimes.Add(stream.ReadString(), stream.ReadInt64());
						}
					}
				}
				catch {
					oldEditTimes.Clear();
				}
			}

			IEnumerable<(string, string)> GetFiles() {

				if (Directory.Exists(RawFilesPath)) {
					foreach (var f in Directory.EnumerateFiles(RawFilesPath, "*.fx", SearchOption.AllDirectories))
						yield return (RawFilesPath, f);
				}
				if (engineContentPath != null) {
					string monoclePath = Path.Combine(engineContentPath, "Effects");
					foreach (var f in Directory.EnumerateFiles(monoclePath, "*", SearchOption.AllDirectories))
						yield return (monoclePath, f);
				}

			}

			foreach (var file in GetFiles()) {
				string localPath = file.Item2.Remove(0, file.Item1.Length + 1);
				long editTime = File.GetLastWriteTime(file.Item2).Ticks;
				currentEditTimes.Add(localPath, editTime);

				if (!oldEditTimes.ContainsKey(localPath) || oldEditTimes[localPath] != editTime)
					dirty = true;
			}

			if (dirty) {

				Console.WriteLine("--- Compiling Shaders");

				using (Process cmd = new Process()) {
					ProcessStartInfo info = new ProcessStartInfo();
					info.FileName = "cmd.exe";
					info.WorkingDirectory = EffectsCompiler;
					info.RedirectStandardInput = true;
					info.RedirectStandardOutput = true;
					info.RedirectStandardError = true;
					info.CreateNoWindow = true;

					cmd.StartInfo = info;
					cmd.Start();


					Task.Factory.StartNew(() => {
						using (var sw = cmd.StandardInput) {

							foreach (var file in GetFiles()) {

								string localPath = file.Item2.Remove(0, file.Item1.Length + 1);
								if ((!oldEditTimes.ContainsKey(localPath) || oldEditTimes[localPath] != currentEditTimes[localPath])) {

									Console.WriteLine($"--- Compiling {localPath}");
									CompileShader(sw, file.Item2, localPath);
								}
							}
						}
					});
					Task.Factory.StartNew(() => {
						using (var sr = cmd.StandardOutput) {
							while (!sr.EndOfStream) {
								/*
								sr.ReadLine();
								/*/
								Console.WriteLine(sr.ReadLine());
								//*/
							}
						}
					});
					using (var sr = cmd.StandardError) {
						bool error = false;
						while (!sr.EndOfStream) {
							var line = sr.ReadLine()!;
							Console.WriteLine(line);
							if (line.Contains("Unexpected error")) {
								error = true;
							}

						}

						if (error) {
							throw new Exception();
						}
					}

				}
				WriteEditTimes();
			}
			else {

				Console.WriteLine($"--- Nothing to compile");
			}

			foreach (var item in oldEditTimes) {
				if (!currentEditTimes.ContainsKey(item.Key)) {

					string a = $"{CompiledPath}\\{Path.ChangeExtension(item.Key, ".cso")}";
					if (File.Exists(a))
						File.Delete(a);
					a = $"{DumpPath}\\{Path.ChangeExtension(item.Key, ".cso")}";
					if (File.Exists(a))
						File.Delete(a);
				}
			}

			foreach (var file in Directory.EnumerateFiles(CompiledPath, "*.cso", SearchOption.AllDirectories)) {

				var name = file.Substring(CompiledPath.Length + 1);
				name = Path.Combine(DumpPath, name);

				if (!Directory.Exists(Path.GetDirectoryName(name)))
					Directory.CreateDirectory(Path.GetDirectoryName(name)!);

				File.Copy(file, name, true);
			}
			currentEditTimes.Clear();
			oldEditTimes.Clear();
		}
		public void CopyFromContent(string regex, string replace = null!) {
			string folder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths[args[0]]))!;
			string contentPath = Path.Combine(folder, "Content");
			DumpPath = Path.Combine(folder, args[1], "Content");

			foreach (var item in Directory.EnumerateFiles(contentPath, "*", SearchOption.AllDirectories)) {
				string sub = item.Replace(contentPath + "\\", "");
				if (sub.StartsWith("obj\\") || sub.StartsWith("bin\\"))
					continue;

				foreach (var f in IgnoredFolders) {
					if (sub.StartsWith(f)) {
						sub = null;
						break;
					}
				}
				if (sub == null)
					continue;

				sub = sub.Replace('\\', '/');

				if (Regex.Match(sub, regex).Success) {
					if (replace != null) {
						sub = Regex.Replace(sub, regex, replace);
					}

					if (!Directory.Exists(Path.GetDirectoryName(Path.Combine(DumpPath, sub)))) {
						Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(DumpPath, sub))!);
					}
					
					File.Copy(item, Path.Combine(DumpPath, sub), true);
				}
			}
		}
		public void CopyFromContent(string regex, Action<string, string, string> onFile) {
			string folder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths[args[0]]))!;
			string contentPath = Path.Combine(folder, "Content");
			DumpPath = Path.Combine(folder, args[1], "Content");

			foreach (var item in Directory.EnumerateFiles(contentPath, "*", SearchOption.AllDirectories)) {
				string sub = item.Replace(contentPath + "\\", "");
				if (sub.StartsWith("obj\\") || sub.StartsWith("bin\\"))
					continue;

				foreach (var f in IgnoredFolders) {
					if (sub.StartsWith(f)) {
						sub = null;
						break;
					}
				}
				if (sub == null)
					continue;

				sub = sub.Replace('\\', '/');

				if (Regex.Match(sub, regex).Success) {
					onFile(contentPath, DumpPath, sub);
				}
			}
		}
		public void CopyFile(string file) {
			CopyFile(file, (a, b, c) => {
				Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(b, c))!);
				File.Copy(Path.Combine(a, c), Path.Combine(b, c), true);
			});
		}
		public void CopyFile(string file, Action<string, string, string> onFile) {
			string folder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths[args[0]]))!;
			string contentPath = Path.Combine(folder, "Content");
			DumpPath = Path.Combine(folder, args[1], "Content");

			file = file.Replace('\\', '/');

			if (File.Exists(Path.Combine(contentPath, file))) {
				onFile(contentPath, DumpPath, file);

			}
		}


		void WriteEditTimes() {

			using (var sw = new BinaryWriter(File.Open(Path.Combine(CompiledPath, "editTime.d"), FileMode.Create))) {
				foreach (var kp in currentEditTimes) {
					sw.Write(kp.Key);
					sw.Write(kp.Value);
				}
			}
		}
		void CopyFiles() {
			foreach (var file in Directory.EnumerateFiles(CompiledPath, "*.bin")) {
				File.Copy(file, Path.Combine(DumpPath, Path.GetFileName(file)), true);
				if (isDebug)
					File.Copy(file, Path.Combine(RawFilesPath, Path.GetFileName(file)), true);
			}
		}

		unsafe byte[] CompileImage(string path, ImageMeta meta) {

			byte[] retval;

			var map = (Bitmap)Image.FromFile(path);
			switch (map.PixelFormat) {
				case PixelFormat.Format32bppArgb:
					break;
				default:
					var newMap = map.Clone(new Rectangle(0, 0, map.Width, map.Height), PixelFormat.Format32bppArgb);
					map.Dispose();
					map = newMap;
					break;	
			}

			var bits = map.LockBits(new Rectangle(0, 0, map.Width, map.Height), ImageLockMode.ReadOnly, map.PixelFormat);

			var ptr = (uint*)bits.Scan0;

			List<uint> palette = new List<uint>() { 0 };
			uint[] data = new uint[map.Width * map.Height];
			var size = new Size(map.Width, map.Height);

			for (int y = 0; y < map.Height; ++y) {
				for (int x = 0; x < map.Width; ++x) {
					var color = ptr[x + y * (bits.Stride >> 2)];
					data[x + y * map.Width] = color;
					if (!palette.Contains(color)) {
						palette.Add(color);
					}
				}
			}

			
			data = ModifyImage(data, ref size, meta, path)!;

			if (meta.byteAmount == 1 && palette.Count <= 255) {
				int offset = (palette.Count << 2) + 6;
				retval = new byte[size.Width * size.Height + offset];

				retval[0] = 0b_0000_0000;

				retval[1] = (byte)(size.Width & 0xFF);
				retval[2] = (byte)(size.Width >> 8);

				retval[3] = (byte)(size.Height & 0xFF);
				retval[4] = (byte)(size.Height >> 8);

				retval[5] = (byte)palette.Count;

				for (int i = 0; i < palette.Count << 2; i += 4) {
					retval[i + 6] = (byte)((palette[i >> 2] >> 16) & 0xFF);
					retval[i + 7] = (byte)((palette[i >> 2] >> 08) & 0xFF);
					retval[i + 8] = (byte)((palette[i >> 2] >> 00) & 0xFF);
					retval[i + 9] = (byte)((palette[i >> 2] >> 24) & 0xFF);
				}

				for (int i = 0; i < data.Length; i++) {
					retval[i + offset] = (byte)palette.IndexOf(data[i]);
				}

			}
			else {
				retval = new byte[1];
				retval[0] = 0b_0000_0001;
				// TODO: Add good PNG compression
			}

			path = path.Remove(0, RawFilesPath.Length + 1);

			path = Path.Combine(CompiledPath, Path.ChangeExtension(path, ".bin"));

			if (!Directory.Exists(Path.GetDirectoryName(path)))
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			File.WriteAllBytes(path, retval);
			

			map.UnlockBits(bits);
			map.Dispose();


			return retval;
		}

		protected virtual uint[]? ModifyImage(uint[] imageData, ref Size size, ImageMeta meta, string path) {

			return imageData;
		}

		void CompileShader(StreamWriter sw, string path, string localPath) {
			if (!Directory.Exists($"{CompiledPath}\\{Path.GetDirectoryName(localPath)}")) {
				Directory.CreateDirectory($"{CompiledPath}\\{Path.GetDirectoryName(localPath)}");
			}
			sw.WriteLine(@$"mgfxc ""{path}"" ""{CompiledPath}\\{Path.ChangeExtension(localPath, ".cso")}""");
		}

		ImageMeta ChangeMeta(string[] meta, ImageMeta original) {

			foreach (var str in meta) {
				string[] split = str.Split(':');
				switch (split[0]) {
					case "type":
						original.type = split[1];
						original.fullData["type"] = split[1];
						break;
					case "byteAmount":
						original.byteAmount = int.Parse(split[1]);
						switch (original.byteAmount) {
							case 1: case 2: case 4:
								break;
							default:
								original.byteAmount = 1;
								break;
						}
						original.fullData["byteAmount"] = original.byteAmount.ToString();
						break;
					case "threeDimensions":
						original.threeDimensions = bool.Parse(split[1]);
						original.fullData["threeDimensions"] = original.threeDimensions.ToString();
						break;
					default:
						original.fullData[split[0]] = split[1];
						break;
				}
			}

			return original;
		}
		void CompileSprites(string path, ImageMeta meta) {

			foreach (var file in Directory.EnumerateFiles(path, "*.png")) {

				var localMeta = File.Exists(file + ".meta") ? ChangeMeta(File.ReadAllLines(file + ".meta"), meta) : meta;

				string localPath = file.Remove(0, RawFilesPath.Length + 1);

				byte[] data;

				if (oldEditTimes.ContainsKey(localPath) && oldEditTimes[localPath] == currentEditTimes[localPath]) {
					using (var stream = new BinaryReader(File.Open(Path.Combine(CompiledPath, Path.ChangeExtension(localPath, ".bin")), FileMode.Open))) {
						data = stream.ReadBytes((int)stream.BaseStream.Length);
					}
				}
				else {
					data = CompileImage(file, localMeta);
				}

				binaryData.Add(localPath, data);
			}
			foreach (var dir in Directory.EnumerateDirectories(path)) {
				var localMeta = File.Exists(dir + ".meta") ? ChangeMeta(File.ReadAllLines(dir + ".meta"), meta) : meta;

				if (File.Exists(dir + ".meta") && (!oldEditTimes.ContainsKey(dir + ".meta") || (oldEditTimes[dir + ".meta"] != currentEditTimes[dir + ".meta"]))) {
					CompileSpritesAlways(dir, localMeta);
				}
				else {
					CompileSprites(dir, localMeta);
				}
			}
		}
		void CompileSpritesAlways(string path, ImageMeta meta) {

			foreach (var file in Directory.EnumerateFiles(path, "*.png")) {

				var localMeta = File.Exists(file + ".meta") ? ChangeMeta(File.ReadAllLines(file + ".meta"), meta) : meta;

				string localPath = file.Remove(0, RawFilesPath.Length + 1);

				binaryData.Add(localPath, CompileImage(file, localMeta));
			}
			foreach (var dir in Directory.EnumerateDirectories(path)) {
				var localMeta = File.Exists(dir + ".meta") ? ChangeMeta(File.ReadAllLines(dir + ".meta"), meta) : meta;

				CompileSpritesAlways(dir, localMeta);
			}
		}
	}

}