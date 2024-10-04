using System.IO;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

//namespace Monocle {
//	public class ModelAtlas {
//		string directory;
//		List<string> models;

//		public MeshData this[string s] {
//			get => MeshData.Import(Path.Combine(s + ".obj"));
//		}

//		public static ModelAtlas FromDirectory(string path) {
//			ModelAtlas effectAtlas = new ModelAtlas();

//			effectAtlas.directory = path;
//			//effectAtlas.effects = new Dictionary<string, MModel>();

//			//foreach (var item in Directory.EnumerateFiles(path)) {
//			//	if (Path.GetExtension(item) != ".obj")
//			//		continue;

//			//	string local = Path.ChangeExtension(Path.GetRelativePath(path, item).Replace('\\', '/'), null);

//			//	effectAtlas.effects.Add(local, MModel.Import(item));
//			//}

//			return effectAtlas;
//		}
//	}
//}
