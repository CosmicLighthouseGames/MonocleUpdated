#pragma warning disable CA1416

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace MonocleCompiler {
	
	public partial class ImageMeta {
		public string type;
		public int byteAmount;
		public bool threeDimensions;

		public void SetData(string[] data)
		{

			foreach (var str in data)
			{
				string[] split = str.Split(':');
				var field = typeof(ImageMeta).GetField(split[0]);

                if (field != null)
				{
					if (field.FieldType == typeof(int))
					{
						field.SetValue(this, Convert.ToInt32(split[1]));
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        field.SetValue(this, Convert.ToSingle(split[1]));
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        field.SetValue(this, split[1]);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        field.SetValue(this, Convert.ToBoolean(split[1]));
                    }
                }
			}
		}

		internal void Copy(ImageMeta other)
		{
			foreach (var field in typeof(ImageMeta).GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				var val = field.GetValue(other);

                field.SetValue(this, val);
			}
		}
	}

	public class CompilerBase {

		public virtual ImageMeta DefaultImageMeta =>
			new ImageMeta()
			{
				type = "image",
				byteAmount = 1,
				threeDimensions = false,
			};

		public string EffectsCompilerPath;


		public bool IsDebug { get; private set; }
		public string ProjectFolder => projectFolder;


		string solutionPath, projectFolder, userContentFolder, engineContentFolder, compiledBinaryFolder, outputFolder;

		public Dictionary<string, string> projectPaths;

		public List<string> IgnoredFolders = new List<string>();


		public CompilerBase(string[] args)
		{
			projectPaths = new Dictionary<string, string>();

			string dir = Assembly.GetExecutingAssembly().Location;

			dir = Path.GetDirectoryName(dir)!;
			while (!string.IsNullOrWhiteSpace(dir) && solutionPath == null) {
				foreach (var path in Directory.GetFiles(dir, "*.sln")) {
					if (path.EndsWith(".sln")) {
						solutionPath = path;
						break;
					}
				}
				dir = Path.GetDirectoryName(dir)!;
			}

			SetValues(args);
		}

		public void SetValues(string[] args) {

			projectPaths.Clear();

			foreach (var line in File.ReadLines(solutionPath))
			{
				var match = Regex.Match(line, @"Project\(""{.+?}""\) = ""(.+?)"", ""(.+?)"".+");
				if (match.Success)
				{
					projectPaths.Add(match.Groups[1].Value, match.Groups[2].Value);
				}
			}

			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "-debug":
						IsDebug = true;
						break;
					case "-project":
						projectFolder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths[args[i + 1]]))!;

						i++;

						break;
					case "-output":
						outputFolder = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, args[i + 1]))!;

						break;
				}
			}

			userContentFolder = Path.Combine(projectFolder, "Content");

			if (projectPaths.ContainsKey("Monocle"))
			{
				engineContentFolder = Path.Combine(Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(solutionPath)!, projectPaths["Monocle"]))!, "Content");
			}
			else
			{
				engineContentFolder = null;
			}
			compiledBinaryFolder = Path.Combine(projectFolder, "obj", "Monocle");

			if (EffectsCompilerPath == null) {
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
										EffectsCompilerPath = Path.GetDirectoryName(file)!;
										break;
									}
								}
							}
						}
					}
				}

				if (EffectsCompilerPath == null)
					throw new Exception();
			}

		}



		public void CompileSprites() {

			Dictionary<string, long> oldEditTimes = new Dictionary<string, long>(),
				currentEditTimes = new Dictionary<string, long>();

			bool dirty = false;

			var RawFilesPath = Path.Combine(userContentFolder, "Graphics");
			var CompiledPath = Path.Combine(compiledBinaryFolder, "Graphics");
			var DumpPath = Path.Combine(outputFolder, "Content", "Graphics");

			if (!Directory.Exists(DumpPath)) {
				Directory.CreateDirectory(DumpPath);
			}
			if (!Directory.Exists(CompiledPath)) {
				Directory.CreateDirectory(CompiledPath);
			}
			else if (File.Exists(Path.Combine(CompiledPath, "editTime.d")) && IsDebug) {
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

			foreach (var file in EnumAllFiles("Graphics", "*.png")) {
				long editTime = File.GetLastWriteTime(file.fullPath).Ticks;
				currentEditTimes.Add(file.localPath, editTime);

				if (dirty || !oldEditTimes.ContainsKey(file.localPath) || oldEditTimes[file.localPath] != editTime)
					dirty = true;
			}
			ImageMeta meta = DefaultImageMeta;

			foreach (var dir in EnumDirectories("Graphics")) {
				using FileStream stream = File.Open(Path.Combine(CompiledPath, Path.GetFileName(dir)) + ".bin", FileMode.Create);
				using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create);

				var localMeta = FileExists(dir + ".meta") ? ChangeMeta(ReadAllLines(dir + ".meta")!, meta) : meta;

				CompileSprites(dir, dir, CompiledPath, archive, localMeta);

			}

			CopyFiles(CompiledPath, DumpPath);

			currentEditTimes.Clear();
			oldEditTimes.Clear();
		}
		public void CompileEffects()
		{
			Dictionary<string, long> oldEditTimes = new Dictionary<string, long>(),
			currentEditTimes = new Dictionary<string, long>();

			bool dirty = false;
			var RawFilesPath = Path.Combine(userContentFolder, "Effects");
			var CompiledPath = Path.Combine(compiledBinaryFolder, "Effects");
			var DumpPath = Path.Combine(outputFolder, "Content", "Effects");

			if (!Directory.Exists(DumpPath)) {
				Directory.CreateDirectory(DumpPath);
			}
			if (!Directory.Exists(CompiledPath)) {
				Directory.CreateDirectory(CompiledPath);
			}
			else if (File.Exists(Path.Combine(CompiledPath, "editTime.d")) && IsDebug) {
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


			foreach (var file in EnumAllFiles("Effects", "*.fx")) {
				long editTime = File.GetLastWriteTime(file.fullPath).Ticks;
				string dir = Path.GetDirectoryName(file.fullPath)!;

				foreach (var line in ReadAllLines(file.localPath)!) {
					var reg = Regex.Match(line, "#include \"(.+?)\"");
					if (reg.Success) {
						string combined = Path.Combine(dir, reg.Groups[1].Value);
						if (File.Exists(combined))
							editTime = Math.Max(editTime, File.GetLastWriteTime(combined).Ticks);
						else
							throw new FileNotFoundException();
					}
				}
				currentEditTimes.Add(file.localPath, editTime);

				if (!oldEditTimes.ContainsKey(file.localPath) || oldEditTimes[file.localPath] != editTime)
					dirty = true;
			}

			if (dirty) {

                string effectCompFolder = Path.Combine(compiledBinaryFolder, "EffectCompile");

                if (Directory.Exists(Path.Combine(compiledBinaryFolder, "EffectCompile")))
				{
					Directory.Delete(Path.Combine(compiledBinaryFolder, "EffectCompile"), true);
				}
				Directory.CreateDirectory(Path.Combine(compiledBinaryFolder, "EffectCompile"));

                foreach (var file in EnumAllFiles("Effects", "*.fx"))
                {
					string path = Path.Combine(compiledBinaryFolder, "EffectCompile", file.localPath.Substring(8));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(file.fullPath, path, true);
                }
                foreach (var file in EnumAllFiles("Effects", "*.fxh"))
                {
                    string path = Path.Combine(compiledBinaryFolder, "EffectCompile", file.localPath.Substring(8));
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(file.fullPath, path, true);
                }

                Console.WriteLine("--- Compiling Shaders");

				using (Process cmd = new Process()) {
					ProcessStartInfo info = new ProcessStartInfo();
					info.FileName = "cmd.exe";
					info.WorkingDirectory = EffectsCompilerPath;
					info.RedirectStandardInput = true;
					info.RedirectStandardOutput = true;
					info.RedirectStandardError = true;
					info.CreateNoWindow = true;

					cmd.StartInfo = info;
					cmd.Start();


					Task.Factory.StartNew(() => {
						try
						{
							using (var sw = cmd.StandardInput)
							{

								foreach (var file in Directory.EnumerateFiles(effectCompFolder, "*.fx", SearchOption.AllDirectories))
								{

									string localPath = file.Substring(effectCompFolder.Length + 1);
									string checkPath = Path.Combine("Effects", localPath);
									if ((!oldEditTimes.ContainsKey(checkPath) || oldEditTimes[checkPath] != currentEditTimes[checkPath]))
									{

										Console.WriteLine($"--- Compiling {localPath}");
										CompileShader(sw, file, $"{CompiledPath}\\{localPath}");
									}
								}
							}
						}
						catch (Exception e) {
							Console.WriteLine(e.ToString());
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
                Directory.Delete(Path.Combine(compiledBinaryFolder, "EffectCompile"), true);
                WriteEditTimes(CompiledPath, currentEditTimes);
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

			var dumpPath = Path.Combine(outputFolder, "Content");

			foreach (var item in Directory.EnumerateFiles(userContentFolder, "*", SearchOption.AllDirectories)) {
				string sub = item.Replace(userContentFolder + "\\", "");
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

					if (!Directory.Exists(Path.GetDirectoryName(Path.Combine(dumpPath, sub)))) {
						Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dumpPath, sub))!);
					}
					
					File.Copy(item, Path.Combine(dumpPath, sub), true);
				}
			}
		}
		public void CopyFromContent(string regex, Action<string, string, string> onFile) {
			var dumpPath = Path.Combine(outputFolder, "Content");

			foreach (var item in Directory.EnumerateFiles(userContentFolder, "*", SearchOption.AllDirectories)) {
				string sub = item.Replace(userContentFolder + "\\", "");
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
					onFile(userContentFolder, dumpPath, sub);
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
			var dumpPath = Path.Combine(outputFolder, "Content");

			file = file.Replace('\\', '/');

			if (File.Exists(Path.Combine(userContentFolder, file))) {
				onFile(userContentFolder, dumpPath, file);

			}
		}


		IEnumerable<string> EnumDirectories(string directory)
		{
			IEnumerable<string> ed(string directory)
			{

				if (Directory.Exists(Path.Combine(userContentFolder, directory)))
				{
					foreach (var dir in Directory.EnumerateDirectories(Path.Combine(userContentFolder, directory)))
					{
						yield return dir.Substring(userContentFolder.Length + 1);
					}
				}
				if (Directory.Exists(Path.Combine(engineContentFolder, directory)))
				{
					foreach (var dir in Directory.EnumerateDirectories(Path.Combine(engineContentFolder, directory)))
					{
						yield return dir.Substring(engineContentFolder.Length + 1);
					}
				}
			}

			foreach (var dir in ed(directory).Distinct())
			{
				yield return dir;
			}


			yield break;
		}
		IEnumerable<string> EnumAllDirectories(string directory)
		{
			IEnumerable<string> ed(string directory)
			{
				foreach (var dir in Directory.EnumerateDirectories(Path.Combine(userContentFolder, directory), "*", SearchOption.AllDirectories))
				{
					yield return dir.Substring(userContentFolder.Length + 1);
				}
				foreach (var dir in Directory.EnumerateDirectories(Path.Combine(engineContentFolder, directory), "*", SearchOption.AllDirectories))
				{
					yield return dir.Substring(engineContentFolder.Length + 1);
				}
			}

			foreach (var dir in ed(directory).Distinct())
			{
				yield return dir;
			}

			yield break;
		}
		IEnumerable<(string fullPath, string localPath)> EnumFiles(string directory, string search = "*")
		{
			if (Directory.Exists(Path.Combine(userContentFolder, directory)))
			{
				foreach (var file in Directory.EnumerateFiles(Path.Combine(userContentFolder, directory), search))
				{
					yield return (file, file.Substring(userContentFolder.Length + 1));
				}
            }
            if (Directory.Exists(Path.Combine(engineContentFolder, directory)))
			{
				foreach (var file in Directory.EnumerateFiles(Path.Combine(engineContentFolder, directory), search))
				{
					yield return (file, file.Substring(engineContentFolder.Length + 1));
				}
			}


			yield break;
		}
		IEnumerable<(string fullPath, string localPath)> EnumAllFiles(string directory, string search = "*")
		{
			foreach (var file in Directory.EnumerateFiles(Path.Combine(userContentFolder, directory), search, SearchOption.AllDirectories))
			{
				yield return (file, file.Substring(userContentFolder.Length + 1));
			}
			foreach (var file in Directory.EnumerateFiles(Path.Combine(engineContentFolder, directory), search, SearchOption.AllDirectories))
			{
				yield return (file, file.Substring(engineContentFolder.Length + 1));
			}

			yield break;
		}

		public string[]? ReadAllLines(string path)
        {
            if (File.Exists(Path.Combine(userContentFolder, path)))
				return File.ReadAllLines(Path.Combine(userContentFolder, path));
            if (File.Exists(Path.Combine(engineContentFolder, path)))
                return File.ReadAllLines(Path.Combine(engineContentFolder, path));
			return null;
		}
		public bool FileExists(string path)
        {
			if (File.Exists(Path.Combine(userContentFolder, path)))
				return true;
			if (File.Exists(Path.Combine(engineContentFolder, path)))
				return true;
			return false;
        }


		void WriteEditTimes(string compiledPath, Dictionary<string, long> editTimes) {

			using (var sw = new BinaryWriter(File.Open(Path.Combine(compiledPath, "editTime.d"), FileMode.Create))) {
				foreach (var kp in editTimes) {
					sw.Write(kp.Key);
					sw.Write(kp.Value);
				}
			}
		}
		void CopyFiles(string from, string to) {
			foreach (var file in Directory.EnumerateFiles(from, "*.bin")) {
				File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);

				Console.WriteLine(Path.Combine(to, Path.GetFileName(file)));
				//if (IsDebug)
				//	File.Copy(file, Path.Combine(RawFilesPath, Path.GetFileName(file)), true);
			}
		}

		unsafe uint[] CompileImage(string path, ImageMeta meta, out Size size) {

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

			uint[] data = new uint[map.Width * map.Height];
			var s = new Size(map.Width, map.Height);

			for (int y = 0; y < map.Height; ++y) {
				for (int x = 0; x < map.Width; ++x) {
					var color = ptr[x + y * (bits.Stride >> 2)];
					data[x + y * map.Width] = color;
				}
			}


			data = ModifyImage(data, ref s, meta, path)!;
			map.Dispose();

			size = s;

			return data;
		}

		protected virtual uint[]? ModifyImage(uint[] imageData, ref Size size, ImageMeta meta, string path) {

			return imageData;
		}

		void CompileShader(StreamWriter sw, string path, string compiledPath) {
			if (!Directory.Exists(Path.GetDirectoryName($"{compiledPath}"))) {
				Directory.CreateDirectory(Path.GetDirectoryName($"{compiledPath}"));
			}
			sw.WriteLine(@$"mgfxc ""{path}"" ""{Path.ChangeExtension(compiledPath, ".cso")}""");
		}

		ImageMeta ChangeMeta(string[] meta, ImageMeta original) {

			var im = new ImageMeta();
			im.Copy(original);
			im.SetData(meta);

			return im;
		}
		unsafe void CompileSprites(string path, string rawPath, string compiledPath, ZipArchive archive, ImageMeta meta) {

			foreach (var file in EnumFiles(path, "*.png")) {

				var localMeta = FileExists(file.localPath + ".meta") ? ChangeMeta(ReadAllLines(file.localPath + ".meta")!, meta) : meta;

				var localPath = file.localPath.Substring(path.Length + 1);

				uint[] data;

				data = CompileImage(file.fullPath, localMeta, out Size size);

				string newPath = Path.Combine(compiledPath, "temp.png");


				using var map = new Bitmap(size.Width, size.Height);

				var bits = map.LockBits(new Rectangle(0, 0, map.Width, map.Height), ImageLockMode.WriteOnly, map.PixelFormat);
				var ptr = (uint*)bits.Scan0;

				for (int y = 0; y < map.Height; ++y) {
					for (int x = 0; x < map.Width; ++x) {
						ptr[x + y * (bits.Stride >> 2)] = data[x + y * map.Width];
					}
				}
				map.Save(newPath);

				map.Dispose();



				ZipArchiveEntry entry = archive.CreateEntry(localPath);

				using BinaryWriter writer = new BinaryWriter(entry.Open());

				writer.Write(File.ReadAllBytes(newPath));

			}
			foreach (var dir in EnumDirectories(path)) {
				var localMeta = FileExists(dir + ".meta") ? ChangeMeta(ReadAllLines(dir + ".meta")!, meta) : meta;

				CompileSprites(dir, rawPath, compiledPath, archive, localMeta);
			}
		}
	}

}