using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle {

	public class FBXNode {

		internal static FBXNode GetFBX(LoadedAsset asset) {


			FBXNode node = new FBXNode();
			node.name = Path.GetFileNameWithoutExtension(asset.Path);

			node.properties = new object[0];
			using BinaryReader br = new BinaryReader(asset.ContentStream);

			if (br.ReadNullTerminatedString() != "Kaydara FBX Binary  ") {
				throw new FormatException();
			}
			br.ReadUInt16();
			uint format = br.ReadUInt32();

			node.children = GetArray(br);

			return node;
		}
		internal static FBXNode GetFBX(string asset) {
			return GetFBX(AssetLoader.GetContent(asset));
		}
		static FBXNode[] GetArray(BinaryReader br) {

			List<FBXNode> retval = new List<FBXNode>();

			while (true) {
				var node = new FBXNode(br);
				if (node.name != null)
					retval.Add(node);
				else
					break;
			}

			return retval.ToArray();
		}

		public string name;

		public object[] properties;
		public FBXNode[] children;

		public FBXNode this[string str] {
			get {
				foreach (var c in children) {
					if (c.name == str)
						return c;
				}
				return null;
			}
		}

		public FBXNode(BinaryReader br) {
			long afterProps = br.BaseStream.Position + 13;

			long skipTo = br.ReadUInt32();
			int propNum = br.ReadInt32();
			int propSize = br.ReadInt32();
			name = br.ReadString();
			afterProps += propSize + name.Length;

			if (skipTo == 0) {
				name = null;
				br.BaseStream.Position = skipTo;
				return;
			}

			properties = new object[propNum];
			for (int i = 0; i < propNum; i++) {

				char type = br.ReadChar();
				object value;


				byte[] decoded;

				void decodeArray(int len) {

					bool compressed = br.ReadInt32() == 1;
					int compressedLen = br.ReadInt32();

					if (compressed) {
						var data = br.ReadBytes(compressedLen);
						using var zlib = new BinaryReader(new ZLibStream(new MemoryStream(data), CompressionMode.Decompress));

						decoded = zlib.ReadBytes(len);
					}
					else {
						decoded = br.ReadBytes(len);
					}
				}

				switch (type) {
					default: {
						throw new FormatException();
					}
					case 'Y':
						value = br.ReadInt16();
						break;
					case 'C':
						value = br.ReadBoolean();
						break;
					case 'I':
						value = br.ReadInt32();
						break;
					case 'F':
						value = br.ReadSingle();
						break;
					case 'D':
						value = br.ReadDouble();
						break;
					case 'L':
						value = br.ReadInt64();
						break;

					case 'f': {
						int len = br.ReadInt32();
						decodeArray(len * 4);

						float[] array = new float[len];
						var ms = new BinaryReader(new MemoryStream(decoded));

						for (int idx = 0; idx < len; idx++) {
							array[idx] = ms.ReadSingle();
						}
						value = array;
					}
					break;
					case 'd': {
						int len = br.ReadInt32();
						decodeArray(len * 8);

						double[] array = new double[len];
						var ms = new BinaryReader(new MemoryStream(decoded));

						for (int idx = 0; idx < len; idx++) {
							array[idx] = ms.ReadDouble();
						}
						value = array;
					}
					break;
					case 'l': {
						int len = br.ReadInt32();
						decodeArray(len * 8);

						long[] array = new long[len];
						var ms = new BinaryReader(new MemoryStream(decoded));

						for (int idx = 0; idx < len; idx++) {
							array[idx] = ms.ReadInt64();
						}
						value = array;
					}
					break;
					case 'i': {
						int len = br.ReadInt32();
						decodeArray(len * 4);

						int[] array = new int[len];
						var ms = new BinaryReader(new MemoryStream(decoded));

						for (int idx = 0; idx < len; idx++) {
							array[idx] = ms.ReadInt32();
						}
						value = array;
					}
					break;
					case 'b': {
						int len = br.ReadInt32();
						decodeArray(len);

						byte[] array = new byte[len];
						var ms = new BinaryReader(new MemoryStream(decoded));

						for (int idx = 0; idx < len; idx++) {
							array[idx] = ms.ReadByte();
						}
						value = array;
					}
					break;

					case 'S':
					case 'R': {
						int len = br.ReadInt32();

						byte[] array = new byte[len];

						for (int idx = 0; idx < len; idx++) {
							array[idx] = br.ReadByte();
						}

						if (type == 'S') {
							value = Encoding.ASCII.GetString(array);
						}
						else {
							value = array;
						}
					}


					break;
				}

				properties[i] = value;
			}

			if (afterProps != skipTo) {
				children = GetArray(br);
			}


			br.BaseStream.Position = skipTo;
		}
		FBXNode() {
		}

		public bool HasChild(string name) {
			foreach (var child in children) {
				if (child.name == name)
					return true;
			}
			return false;
		}

		public override string ToString() {
			return name;
		}
	}

	/// <summary>
	/// What I need:
	/// - Bone structure in the scene class
	/// 
	/// </summary>
	
	public class MonocleBone {

		List<MonocleBone> children;
		public MonocleBone Parent { get; private set; }
		MonocleArmature armature;

		public string Name { get; private set; }

		Vector3 Position_orig;
		Quaternion Rotation_orig;

		public Vector3 LocalPosition, LocalScale;

		public Vector2 LocalShearX, LocalShearY, LocalShearZ;

		Quaternion rotation, localLocalRotation;
		public Quaternion LocalRotation {
			get => rotation; 
			set {
				rotation = value;
				localLocalRotation = Quaternion.Inverse(Rotation_orig) * value * Rotation_orig;
			}
		}

		public Matrix Transform {
			get {
				Vector3 globalOffset = GlobalOffset(LocalPosition);

				var mat = Matrix.CreateTranslation(-Position_orig) * GlobalTransform() * Matrix.CreateTranslation(Position_orig);

				return mat;
			}
		}
		public Matrix LocalTransform {
			get {
				var mat = new Matrix(
					LocalScale.X, 0, 0, 0,
					0, LocalScale.Y, 0, 0,
					0, 0, LocalScale.Z, 0,
					0, 0, 0, 1)
					* Matrix.CreateFromQuaternion(localLocalRotation);

				mat *= Matrix.CreateTranslation(LocalPosition);

				return mat;
			}
			set {

				var val = value;

				LocalPosition = val.Translation;
				val.Translation = Vector3.Zero;

				localLocalRotation = Quaternion.CreateFromRotationMatrix(val);
				localLocalRotation.Normalize();
				val *= Matrix.CreateFromQuaternion(Quaternion.Inverse(localLocalRotation));

				LocalScale = Calc.Snap(new Vector3(val.M11, val.M22, val.M33), 0.001f);

				LocalShearX = Vector2.Zero;
				LocalShearY = Vector2.Zero;
				LocalShearZ = Vector2.Zero;
			}
		}

		private Matrix GlobalTransform() {

			Matrix mat;

			mat = Matrix.CreateScale(LocalScale) * Matrix.CreateFromQuaternion(localLocalRotation);
			
			if (Parent != null) {
				mat *= Parent.GlobalTransform();
			}

			return mat;
		}
		private Vector3 GlobalOffset(Vector3 pos) {


			if (Parent != null) {
				Matrix mat = Parent.GlobalTransform();
				Vector3 parentPos = Parent.GlobalOffset(Parent.LocalPosition);
				pos = Vector3.Transform(pos, mat);
				pos += parentPos;
			}

			return pos;
		}


		public MonocleBone(string name) {
			Name = name;
			children = new List<MonocleBone>();

			LocalScale = Vector3.One;
			rotation = Quaternion.Identity;
			Rotation_orig = Quaternion.Identity;
		}
		public MonocleBone(string name, Matrix matrix) {
			Name = name;
			children = new List<MonocleBone>();

			LocalScale = Vector3.One;
			rotation = Quaternion.Identity;
			Rotation_orig = Quaternion.CreateFromRotationMatrix(matrix);
			Rotation_orig.Normalize();
			Position_orig = matrix.Translation;
		}


		internal void AddChild(MonocleBone child) {
			child.Parent = this;
			children.Add(child);
			child.SetArmature(armature);
		}
		internal void SetArmature(MonocleArmature armature) {
			if (this.armature != armature) {
				if (this.armature != null) {
					this.armature.RemoveBone(this);
				}
				this.armature = armature;
				armature.AddBone(this);
			}
			foreach (var child in children) {
				child.SetArmature(armature);
			}
		}

		public IEnumerable<MonocleBone> EnumerateChildren() {
			return children.AsEnumerable();
		}
	}
	public class MonocleArmature {

		Dictionary<string, MonocleBone> bones;

		public MonocleBone this[string name] {
			get => bones[name];
		}


		public MonocleArmature() {
			bones = new Dictionary<string, MonocleBone>();
		}

		internal void AddBone(MonocleBone bone) {
			if (!bones.ContainsKey(bone.Name))
				bones.Add(bone.Name, bone);
			bone.SetArmature(this);
		}
		internal void RemoveBone(MonocleBone bone) {
			bones.Remove(bone.Name);
		}

		public MonocleBone GetBone(string name) { 
			return bones[name];
		}

		public IEnumerable<TransformVertex> Transform(MonocleModel mesh) {

			foreach (var bone in bones.Values) {
				void transform(int index, ref MonocleVertex vertex) {
					var matrix = bone.Transform;
					float factor = mesh.GetBalancedWeight(bone.Name, index);
					vertex.Position = Vector3.Lerp(vertex.Position, Vector3.Transform(vertex.Position, matrix), factor);
					vertex.Normal = Vector3.Lerp(vertex.Normal, Vector3.TransformNormal(vertex.Normal, matrix), factor);
				}

				yield return transform;
			}

			yield break;
		}
	}

	public class FBXScene {
		public static FBXScene Import(LoadedAsset content) {

			FBXScene retval = new FBXScene();

			retval.meshes = new Dictionary<string, (MonocleVertex[], int[][])>();
			retval.armatures = new Dictionary<string, MonocleArmature>();
			retval.weights = new Dictionary<string, Dictionary<string, float[]>>();

			var array = FBXNode.GetFBX(content);

			int[] pvi = null;

			IEnumerable GetArray<T>(FBXNode node, string name) {

				if (node[name] == null) {
					node = node[$"LayerElement{name}"];
				}
				if (node == null)
					yield break;

				var data = node[name];
				var indices = node[$"{name}Index"];
				if (data == null)
					data = node[$"{name}s"];
				if (indices == null)
					indices = node[$"{name}sIndex"];
				if (data == null || indices == null)
					yield break;

				if (typeof(T) == typeof(Vector2) || typeof(T) == typeof(Vector3) ||typeof(T) == typeof(Vector4)) {
					float[] fArray;
					if (data.properties[0] is double[]) {
						fArray = ((double[])data.properties[0]).Select(x => (float)x).ToArray();
					}
					else if (data.properties[0] is long[]) {
						fArray = ((long[])data.properties[0]).Select(x => (float)x).ToArray();
					}
					else if (data.properties[0] is int[]) {
						fArray = ((int[])data.properties[0]).Select(x => (float)x).ToArray();
					}
					else {
						fArray = (float[])data.properties[0];
					}

					int[] iArray = (int[])indices.properties[0];

					if (node["MappingInformationType"].properties[0].ToString() == "ByVertice") {
						int[] newArray = new int[pvi.Length];

						for (int i = 0; i < pvi.Length; i++) {
							int v = pvi[i];
							if (v < 0)
								v = ~v;
							newArray[i] = iArray[v];
						}

						iArray = newArray;
					}

					if (typeof(T) == typeof(Vector2)) {
						for (int i = 0; i < iArray.Length; i++) {
							int idx = iArray[i] * 2;
							yield return new Vector2(fArray[idx], fArray[idx + 1]);
						}
					}
					else if (typeof(T) == typeof(Vector3)) {
						for (int i = 0; i < iArray.Length; i++) {
							int idx = iArray[i] * 3;
							yield return new Vector3(fArray[idx], fArray[idx + 1], fArray[idx + 2]);
						}
					}
					else if (typeof(T) == typeof(Vector4)) {
						for (int i = 0; i < iArray.Length; i++) {
							int idx = iArray[i] * 4;
							yield return new Vector4(fArray[idx], fArray[idx + 1], fArray[idx + 2], fArray[idx + 3]);
						}
					}
				}


				yield break;
			}

			var objects = array["Objects"];

			//Dictionary<long, (MonocleVertex[] verts, int[][] indices, int[] mapping)> meshes = new Dictionary<long, (MonocleVertex[] verts, int[][] indices, int[] mapping)>();

			//Dictionary<long, MonocleBone> boneIDs = new Dictionary<long, MonocleBone>();
			//Dictionary<long, MonocleArmature> armatureIDs = new Dictionary<long, MonocleArmature>();
			Dictionary<long, FBXNode> nodeIDs = new Dictionary<long, FBXNode>();

			//Queue<(long, Matrix)> boneTransforms = new Queue<(long, Matrix)>();
			//Queue<string> meshNames = new Queue<string>();
			//Queue<MonocleArmature> armatureIDQueue = new Queue<MonocleArmature>();

			Dictionary<long, List<long>> parent2Child = new Dictionary<long, List<long>>();
			Dictionary<long, List<long>> child2Parent = new Dictionary<long, List<long>>();

			List<FBXNode> meshes = new List<FBXNode>();
			List<FBXNode> armatures = new List<FBXNode>();
			IEnumerable<(string, float[])> getWeights(List<MonocleVertex> comp, int[] mapping, FBXNode parentNode) {


				if (!parent2Child.ContainsKey((long)parentNode.properties[0]))
					yield break;

				foreach (var val in parent2Child[(long)parentNode.properties[0]]) {
					var node = nodeIDs[val];

					if (node.name == "Model" || node.properties[2] as string == "LimbNode") {
						foreach (var weight in getWeights(comp, mapping, node)) {
							yield return weight;
						}
					}
					if (node.name != "Deformer" || node.properties[2] as string != "Cluster")
						continue;



					var indices = node["Indexes"].properties[0] as int[];
					var weights = node["Weights"].properties[0] as double[];

					float[] values = new float[comp.Count];

					for (int i = 0; i < values.Length; i++) {
						values[i] = 0;
						int index = Array.IndexOf(indices, mapping[i]);
						if (index >= 0) {
							values[i] = (float)weights[index];
						}
					}

					yield return ((node.properties[1] as string).Split("\0\u0001")[0], values);
					foreach (var weight in getWeights(comp, mapping, node)) {
						yield return weight;
					}

				}
			}
			object compileNode(FBXNode child) {

				string name = ((string)child.properties[1]).Split("\0\u0001")[0];
				var type = ((string)child.properties[2]);

				long uuid = (long)child.properties[0];

				switch (child.name) {
					default:
						break;
					case "Pose":
						break;
					case "Deformer": {

						if (type == "Skin") {

							List<float[]> weights = new List<float[]>();
							if (parent2Child.ContainsKey(uuid)) {
								foreach (var val in parent2Child[uuid]) {
									var node = nodeIDs[val];

								}
							}

							return weights.ToArray();
						}
						else if (type == "SubDeformer" && child.HasChild("Indexes")) {

						}
						else {

						}

						break;
					}
					case "Material":
					case "Texture":
					case "Video":
						break;
					case "NodeAttribute": {

						switch ((string)child.properties[2]) {
							case "Null":
								//armatureIDQueue.Enqueue(armature);
								if (parent2Child.ContainsKey(uuid)) {
									foreach (var val in parent2Child[uuid]) {
										var node = nodeIDs[val];
										var matrix = compileNode(node);
									}
								}
								break;
							case "LimbNode": {
								(long, Matrix) mat;
								//if (!boneTransforms.TryDequeue(out mat)) {
								//	mat = (-1, Matrix.Identity);
								//}

								//boneIDs.Add((long)child.properties[0], bone);
								//if (mat.Item1 >= 0) {
								//	boneIDs.Add(mat.Item1, bone);
								//}


								break;
							}
							default:
								break;
						}

						break;
					}
					case "Geometry": {

						var dArray = child["Vertices"].properties[0] as double[];
						Vector3[] verts = new Vector3[dArray.Length / 3];
						for (int i = 0; i < dArray.Length; i += 3) {
							verts[i / 3] = new Vector3((float)dArray[i], (float)dArray[i + 2], -(float)dArray[i + 1]);
						}

						List<Vector3> normals = new List<Vector3>();

						pvi = child["PolygonVertexIndex"].properties[0] as int[];

						MonocleVertex[] data = new MonocleVertex[pvi.Length];

						// Triangulating faces
						List<int> faceCount = new List<int>();
						int c = 0;
						for (int i = 0; i < data.Length; i++) {
							c++;
							int index = pvi[i];
							if (index < 0) {
								index = ~index;
								faceCount.Add(c);
								c = 0;
							}
							data[i].Position = verts[index];
						}

						// Other properties
						int idx = 0;
						if (child[$"LayerElementColor"] == null) {

							for (int i = 0; i < data.Length; i++) {
								data[i].Color = Vector4.One;
							}
						}
						else {
							foreach (Vector4 item in GetArray<Vector4>(child, "Color")) {
								data[idx++].Color = item;
							}
						}
						idx = 0;
						foreach (Vector2 item in GetArray<Vector2>(child, "UV")) {
							data[idx++].TextureCoordinate = new Vector2(item.X, 1 - item.Y);
						}
						idx = 0;
						foreach (Vector3 item in GetArray<Vector3>(child, "Normal")) {
							data[idx++].Normal = new Vector3(item.X, item.Z, -item.Y);
						}

						List<MonocleVertex> comp = new List<MonocleVertex>(data.Distinct());
						List<List<int>> finalIndices = new List<List<int>>();

						// Material Mapping
						string mappingType = "AllSame";
						if (child.HasChild("LayerElementMaterial")) {
							mappingType = child["LayerElementMaterial"]["MappingInformationType"].properties[0].ToString();
						}
						switch (mappingType) {
							default:
							case "AllSame": {

								List<int> indices = new List<int>();
								idx = 0;
								for (int i = 0; i < faceCount.Count; i++) {
									for (int j = 0; j < faceCount[i] - 2; j++) {
										indices.Add(comp.IndexOf(data[idx]));
										indices.Add(comp.IndexOf(data[idx + j + 2]));
										indices.Add(comp.IndexOf(data[idx + j + 1]));
									}
									idx += faceCount[i];
								}
								finalIndices.Add(indices);
								break;
							}
							case "ByPolygon": {
								var exChild = child["LayerElementMaterial"];

								idx = 0;


								int[] matArray = exChild["Materials"].properties[0] as int[];

								for (int i = 0; i < faceCount.Count; i++) {
									int index = matArray[i];
									while (finalIndices.Count <= index) {
										finalIndices.Add(new List<int>());
									}
									for (int j = 0; j < faceCount[i] - 2; j++) {
										finalIndices[index].Add(comp.IndexOf(data[idx]));
										finalIndices[index].Add(comp.IndexOf(data[idx + j + 2]));
										finalIndices[index].Add(comp.IndexOf(data[idx + j + 1]));
									}
									idx += faceCount[i];
								}
								break;
							}
						}

						int[] mapping = new int[comp.Count];

						for (int i = 0; i < comp.Count; i++) {
							mapping[i] = Array.IndexOf(verts, comp[i].Position);
						}

						List<object> retval = new List<object>(){
							comp.ToArray(),
							finalIndices.Select((l) => { return l.ToArray(); }).ToArray(),
						};

						if (parent2Child.ContainsKey(uuid)) {
							foreach (var val in parent2Child[uuid]) {
								var node = nodeIDs[val];
								if (node.name == "Deformer" && node.properties[2] as string == "Skin") {
									foreach (var value in getWeights(comp, mapping, node)) {
										retval.Add(value);
									}
									//getWeights(comp, mapping, node);
								}
							}
						}

						return retval.ToArray();
					}
					case "Model": {

						switch (type) {
							case "LimbNode": {
								var properties = child["Properties70"];

								var matrix = Matrix.Identity;

								foreach (var pr in properties.children) {
									switch (pr.properties[0].ToString()) {
										case "Lcl Translation":
											matrix *= Matrix.CreateTranslation(new Vector3(Convert.ToSingle(pr.properties[4]), Convert.ToSingle(pr.properties[5]), Convert.ToSingle(pr.properties[6])));
											break;
										case "Lcl Rotation":
											matrix *= Matrix.CreateFromQuaternion(Calc.EulerAngle(MathHelper.ToRadians(Convert.ToSingle(pr.properties[4])), MathHelper.ToRadians(Convert.ToSingle(pr.properties[5])), MathHelper.ToRadians(Convert.ToSingle(pr.properties[6]))));
											break;
										case "Lcl Scaling":
											matrix *= Matrix.CreateScale(new Vector3(Convert.ToSingle(pr.properties[4]), Convert.ToSingle(pr.properties[5]), Convert.ToSingle(pr.properties[6])));
											break;
									}
								}
								var bone = new MonocleBone(name, matrix);

								if (parent2Child.ContainsKey(uuid)) {
									foreach (var val in parent2Child[uuid]) {
										var node = nodeIDs[val];
										if (node.name == "Model") {
											var childBone = compileNode(node) as MonocleBone;
											if (childBone != null) {
												bone.AddChild(childBone);
											}
										}
									}
								}

								return bone;

								break;
							}
						}
						break;
					}
				}

				return null;
			}

			foreach (var child in objects.children) {
				nodeIDs.Add((long)child.properties[0], child);
				if (child.name == "Model") {
					if ((string)child.properties[2] == "Mesh") {
						meshes.Add(child);
					}
					else if ((string)child.properties[2] == "Null") {
						armatures.Add(child);
					}
				}
				//else if (child.name == "Deformer" && (string)child.properties[2] == "Skin") {
				//	deformers.Add(child);
				//}
			}
			foreach (var child in array["Connections"].children) {
				long parentID = (long)child.properties[2];
				long childID = (long)child.properties[1];

				if (parentID != 0) {
					if (!parent2Child.ContainsKey(parentID))
						parent2Child[parentID] = new List<long>();
					parent2Child[parentID].Add(childID);

					if (!child2Parent.ContainsKey(childID))
						child2Parent[childID] = new List<long>();
					child2Parent[childID].Add(parentID);
				}
			}



			foreach (var mesh in meshes) {
				long uuid = (long)mesh.properties[0];
				string[] split = ((string)mesh.properties[1]).Split("\0\u0001");
				var type = ((string)mesh.properties[2]);

				object[] meshData = null!;

				if (parent2Child.ContainsKey(uuid)) {
					foreach (var val in parent2Child[uuid]) {
						var node = nodeIDs[val];
						switch (node.name) {
							case "Geometry":
								meshData = compileNode(node) as object[];
								break;
						}
					}
				}
				if (child2Parent.ContainsKey(uuid)) {
					foreach (var val in child2Parent[uuid]) {
						var node = nodeIDs[val];
					}
				}

				retval.meshes[split[0]] = ((MonocleVertex[])meshData[0], (int[][])meshData[1]);
				var dict = retval.weights[split[0]] = new Dictionary<string, float[]>();

				for (int i = 2; i < meshData.Length; i++) {
					(string, float[]) weights = ((string, float[]))meshData[i];
					dict.Add(weights.Item1, weights.Item2);
				}
			}

			foreach (var arm in armatures) {
				long uuid = (long)arm.properties[0];
				string[] split = ((string)arm.properties[1]).Split("\0\u0001");
				var type = ((string)arm.properties[2]);

				var armature = retval.armatures[split[0]] = new MonocleArmature();

				if (parent2Child.ContainsKey(uuid)) {
					foreach (var val in parent2Child[uuid]) {
						var node = nodeIDs[val];
						if (node.name == "Model") {
							var bone = compileNode(node) as MonocleBone;
							armature.AddBone(bone);
						}
					}
				}
			}

			//foreach (var child in objects.children) {



			//	//switch (child.name) {
			//	//	case "Deformer": {

			//	//		if (type == "Skin") {
			//	//		}
			//	//		else if (type == "SubDeformer" && child.HasChild("Indexes")) {

			//	//		}
			//	//		else {

			//	//		}

			//	//		break;
			//	//	}
			//	//	case "Model":

			//	//		switch (type) {
			//	//			case "Mesh":
			//	//				break;
			//	//			case "Null":
			//	//				break;
			//	//			default:
			//	//				break;
			//	//		}
			//	//		break;
			//	//}
			//}

			return retval;
		}

		Dictionary<string, (MonocleVertex[], int[][])> meshes;
		Dictionary<string, Dictionary<string, float[]>> weights;

		Dictionary<string, MonocleArmature> armatures;

		FBXScene() {

		}

		public MonocleModel GetMesh(string mesh) {
			var pair = meshes[mesh];
			MonocleVertex[] verts = new MonocleVertex[pair.Item1.Length];
			Array.Copy(pair.Item1, verts, pair.Item1.Length);
			int[][] indices = new int[pair.Item2.Length][];
			for (int i = 0; i < indices.Length; i++) {
				indices[i] = new int[pair.Item2[i].Length];
				Array.Copy(pair.Item2[i], indices[i], pair.Item2[i].Length);
			}

			Dictionary<string, float[]> weights = new Dictionary<string, float[]>();
			if (this.weights.ContainsKey(mesh)) {
				var toCopy = this.weights[mesh];
				foreach (var key in toCopy.Keys) {
					float[] array = new float[toCopy[key].Length];
					Array.Copy(toCopy[key], array, array.Length);
					weights.Add(key, array);
				}
			}
			var retval = new MonocleModel(verts, indices, weights);

			retval.CalculateTangent();

			return retval;
		}
		public Dictionary<string, MonocleModel> GetMeshes() {
			Dictionary<string, MonocleModel> retval = new Dictionary<string, MonocleModel>();

			foreach (var key in meshes.Keys) {
				retval[key] = GetMesh(key);
			}

			return retval;
		}
		public MonocleArmature GetArmature(string armature) {
			return armatures[armature];
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct MonocleVertex : IVertexType {

		/// <summary>
		/// Position.
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// Normal.
		/// </summary>
		public Vector3 Normal;

		/// <summary>
		/// Tangent.
		/// </summary>
		public Vector3 Tangent;

		/// <summary>
		/// Binormal.
		/// </summary>
		public Vector3 Binormal;

		/// <summary>
		/// Texture coords.
		/// </summary>
		public Vector2 TextureCoordinate;

		/// <summary>
		/// Texture coords.
		/// </summary>
		public Vector4 Color;

		/// <summary>
		/// Vertex declaration object.
		/// </summary>
		public static readonly VertexDeclaration VertexDeclaration;

		/// <summary>
		/// Vertex declaration.
		/// </summary>
		VertexDeclaration IVertexType.VertexDeclaration {
			get {
				return VertexDeclaration;
			}
		}

		/// <summary>
		/// Static constructor to init vertex declaration.
		/// </summary>
		static MonocleVertex() {
			VertexElement[] elements = new VertexElement[] {
				new VertexElement(0 * 4, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
				new VertexElement(3 * 4, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
				new VertexElement(6 * 4, VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
				new VertexElement(9 * 4, VertexElementFormat.Vector3, VertexElementUsage.Binormal, 0),
				new VertexElement(12 * 4, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
				new VertexElement(14 * 4, VertexElementFormat.Vector4, VertexElementUsage.Color, 0)
			};
			VertexDeclaration declaration = new VertexDeclaration(elements);
			VertexDeclaration = declaration;
		}


		/// <summary>
		/// Create the vertex.
		/// </summary>
		/// <param name="position">Vertex position.</param>
		/// <param name="normal">Vertex normal.</param>
		/// <param name="textureCoordinate">Texture coordinates.</param>
		/// <param name="tangent">Vertex tangent.</param>
		/// <param name="binormal">Vertex binormal.</param>
		public MonocleVertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector3 binormal, Vector2 textureCoordinate, Vector4 color) {
			Normal = normal;
			Position = position;
			TextureCoordinate = textureCoordinate;
			Tangent = tangent;
			Binormal = binormal;
			Color = color;
		}

		/// <summary>
		/// Get if equals another object.
		/// </summary>
		/// <param name="obj">Object to compare to.</param>
		/// <returns>If objects are equal.</returns>
		public override bool Equals(object obj) {
			if (obj == null) {
				return false;
			}
			if (obj.GetType() != base.GetType()) {
				return false;
			}
			return (this == ((MonocleVertex)obj));
		}

		/// <summary>
		/// Get the hash code of this vertex.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode() {
			unchecked {
				var hashCode = Position.GetHashCode();
				hashCode = (hashCode * 397) ^ Normal.GetHashCode();
				hashCode = (hashCode * 397) ^ TextureCoordinate.GetHashCode();
				hashCode = (hashCode * 397) ^ Tangent.GetHashCode();
				hashCode = (hashCode * 397) ^ Binormal.GetHashCode();
				return hashCode;
			}
		}

		/// <summary>
		/// Return string representation of this vertex.
		/// </summary>
		/// <returns>String representation of the vertex.</returns>
		public override string ToString() {
			return "{{Position:" + this.Position + " Normal:" + this.Normal + " TextureCoordinate:" + this.TextureCoordinate + " Tangent " + this.Tangent + "}}";
		}

		/// <summary>
		/// Return if two vertices are equal.
		/// </summary>
		/// <param name="left">Left side to compare.</param>
		/// <param name="right">Right side to compare.</param>
		/// <returns>If equal.</returns>
		public static bool operator ==(MonocleVertex left, MonocleVertex right) {
			return (((left.Position == right.Position) && (left.Normal == right.Normal)) && (left.TextureCoordinate == right.TextureCoordinate) && left.Binormal == right.Binormal && left.Tangent == right.Tangent);
		}

		/// <summary>
		/// Return if two vertices are not equal.
		/// </summary>
		/// <param name="left">Left side to compare.</param>
		/// <param name="right">Right side to compare.</param>
		/// <returns>If not equal.</returns>
		public static bool operator !=(MonocleVertex left, MonocleVertex right) {
			return !(left == right);
		}
	}

	public class MonocleModel {

		public static void RecalculateNormals(MonocleVertex[] vertices) {

			var indices = new short[vertices.Length];
			for (int i = 0; i < vertices.Length; i++) {
				indices[i] = (short)indices[i];
			}
			RecalculateNormals(vertices, indices);
		}
		public static void CalculateTangent(MonocleVertex[] vertices) {

			var indices = new short[vertices.Length];
			for (int i = 0; i < vertices.Length; i++) {
				indices[i] = (short)indices[i];
			}
			CalculateTangent(vertices, indices);
		}
		public static void RecalculateNormals(MonocleVertex[] vertices, short[] indices) {

			Vector3[] newNormals = new Vector3[vertices.Length];
			Vector3[] newBi = new Vector3[vertices.Length];
			Vector3[] newTan = new Vector3[vertices.Length];

			for (int i = 0; i < indices.Length; i += 3) {
				Vector3 a = vertices[indices[i]].Position;
				Vector3 b = vertices[indices[i + 1]].Position;
				Vector3 c = vertices[indices[i + 2]].Position;

				Vector3 n = -Vector3.Cross(b - a, c - a).SafeNormalize();

				Vector2 h = vertices[indices[i]].TextureCoordinate;
				Vector2 k = vertices[indices[i + 1]].TextureCoordinate;
				Vector2 l = vertices[indices[i + 2]].TextureCoordinate;

				Vector3 d = a - b;
				Vector3 e = a - c;
				Vector2 f = h - k;
				Vector2 g = h - l;

				Vector3 tan, bi;

				Matrix u1 = new Matrix(){
					M11 = g.X,
					M12 = -f.X,
					M21 = -g.Y,
					M22 = f.Y,
				};
				Matrix u2 = new Matrix(){
					M11 = d.X,
					M12 = d.Y,
					M13 = d.Z,
					M21 = e.X,
					M22 = e.Y,
					M23 = e.Z,
				};

				u1 *= u2;

				tan = new Vector3(u1.M11, u1.M12, u1.M13).SafeNormalize();// tan.SafeNormalize();
				bi = new Vector3(u1.M21, u1.M22, u1.M23).SafeNormalize();// tan.SafeNormalize();
																		 //bi = bi.SafeNormalize();

				for (int j = 0; j < 3; ++j) {
					newNormals[indices[i + j]] += n;
					newTan[indices[i + j]] -= tan;
					newBi[indices[i + j]] -= bi;
				}
			}

			for (int i = 0; i < vertices.Length; i++) {

				vertices[i].Normal = newNormals[i].SafeNormalize();
				vertices[i].Binormal = newBi[i].SafeNormalize();
				vertices[i].Tangent = newTan[i].SafeNormalize();
			}
		}
		public static void CalculateTangent(MonocleVertex[] vertices, short[] indices) {

			Vector3[] newBi = new Vector3[vertices.Length];
			Vector3[] newTan = new Vector3[vertices.Length];

			for (int i = 0; i < indices.Length; i += 3) {
				Vector3 a = vertices[indices[i]].Position;
				Vector3 b = vertices[indices[i + 1]].Position;
				Vector3 c = vertices[indices[i + 2]].Position;

				Vector2 h = vertices[indices[i]].TextureCoordinate;
				Vector2 k = vertices[indices[i + 1]].TextureCoordinate;
				Vector2 l = vertices[indices[i + 2]].TextureCoordinate;

				Vector3 d = a - b;
				Vector3 e = a - c;
				Vector2 f = h - k;
				Vector2 g = h - l;

				Vector3 tan, bi;

				Matrix u1 = new Matrix(){
					M11 = g.X,
					M12 = -f.X,
					M21 = -g.Y,
					M22 = f.Y,
				};
				Matrix u2 = new Matrix(){
					M11 = d.X,
					M12 = d.Y,
					M13 = d.Z,
					M21 = e.X,
					M22 = e.Y,
					M23 = e.Z,
				};

				u1 *= u2;

				tan = new Vector3(u1.M11, u1.M12, u1.M13).SafeNormalize();
				bi = new Vector3(u1.M21, u1.M22, u1.M23).SafeNormalize();

				for (int j = 0; j < 3; ++j) {
					newTan[indices[i + j]] -= tan;
					newBi[indices[i + j]] -= bi;
				}
			}

			for (int i = 0; i < vertices.Length; i++) {
				vertices[i].Binormal = newBi[i].SafeNormalize();
				vertices[i].Tangent = newTan[i].SafeNormalize();
			}
		}
		public static void RecalculateNormals(MonocleVertex[] vertices, short[][] indices) {
			List<short> newInds = new List<short>();

			foreach (var item in indices) {
				foreach (var i in item) {
					newInds.Add(i);
				}
			}

			RecalculateNormals(vertices, newInds.ToArray());

		}
		public static void CalculateTangent(MonocleVertex[] vertices, short[][] indices) {
			List<short> newInds = new List<short>();

			foreach (var item in indices) {
				foreach (var i in item) {
					newInds.Add(i);
				}
			}

			CalculateTangent(vertices, newInds.ToArray());
		}

		public static MonocleModel CreateCube(float size, bool invert, bool smooth = false) {

			size /= 2;

			Vector3[] verts;
			int[] tris;
			float xScale = invert ? -1 : 1;

			if (smooth) {
				verts = new Vector3[8];
				tris = new int[]{
					7, 6, 4, // Top
					5, 7, 4,
					1, 0, 2, // Bottom
					3, 1, 2,

					0, 1, 5, // Back
					4, 0, 5,
					3, 2, 6, // Front
					7, 3, 6,

					2, 0, 4, // Left
					6, 2, 4,
					1, 3, 7, // Right
					5, 1, 7,
				};

				int idx = 0;
				for (int a = -1; a <= 1; a += 2)
					for (int b = -1; b <= 1; b += 2)
						for (int c = -1; c <= 1; c += 2) {
							verts[idx++] = new Vector3(c * size, a * size * xScale, b * size);
						}
			}
			else {
				verts = new Vector3[24];
				tris = new int[]{
					03, 00, 06, // Top
					09, 03, 06,
					21, 18, 12, // Bottom
					15, 21, 12,

					01, 04, 16, // Back
					13, 01, 16,
					10, 07, 19, // Front
					22, 10, 19,

					08, 02, 14, // Left
					20, 08, 14,
					05, 11, 23, // Right
					17, 05, 23,
				};

				int idx = 0;
				for (int a = -1; a <= 1; a += 2)
					for (int b = -1; b <= 1; b += 2)
						for (int c = -1; c <= 1; c += 2) {
							verts[idx++] = new Vector3(c * size, a * size * xScale, b * size);
							verts[idx++] = new Vector3(c * size, a * size * xScale, b * size);
							verts[idx++] = new Vector3(c * size, a * size * xScale, b * size);
						}

			}

			var md = new MonocleModel(verts, tris);
			md.RecalculateNormals();

			return md;
		}
		public static MonocleModel CreateCube(Vector3 size, bool invert, bool smooth = false) {

			size /= 2;

			Vector3[] verts;
			int[] tris;
			float xScale = invert ? -1 : 1;

			if (smooth) {
				verts = new Vector3[8];
				tris = new int[]{
					7, 6, 4, // Top
					5, 7, 4,
					1, 0, 2, // Bottom
					3, 1, 2,

					0, 1, 5, // Back
					4, 0, 5,
					3, 2, 6, // Front
					7, 3, 6,

					2, 0, 4, // Left
					6, 2, 4,
					1, 3, 7, // Right
					5, 1, 7,
				};

				int idx = 0;
				for (int a = -1; a <= 1; a += 2)
					for (int b = -1; b <= 1; b += 2)
						for (int c = -1; c <= 1; c += 2) {
							verts[idx++] = new Vector3(c * size.X, a * size.Y * xScale, b * size.Z);
						}
			}
			else {
				verts = new Vector3[24];
				tris = new int[]{
					03, 00, 06, // Top
					09, 03, 06,
					21, 18, 12, // Bottom
					15, 21, 12,

					01, 04, 16, // Back
					13, 01, 16,
					10, 07, 19, // Front
					22, 10, 19,

					08, 02, 14, // Left
					20, 08, 14,
					05, 11, 23, // Right
					17, 05, 23,
				};

				int idx = 0;
				for (int a = -1; a <= 1; a += 2)
					for (int b = -1; b <= 1; b += 2)
						for (int c = -1; c <= 1; c += 2) {
							verts[idx++] = new Vector3(c * size.X, a * size.Y * xScale, b * size.Z);
							verts[idx++] = new Vector3(c * size.X, a * size.Y * xScale, b * size.Z);
							verts[idx++] = new Vector3(c * size.X, a * size.Y * xScale, b * size.Z);
						}

			}

			var md = new MonocleModel(verts, tris);
			md.RecalculateNormals();

			return md;
		}

		//public static Dictionary<string, MonocleModel> ImportMeshes(FBXNode node) {


		//	int[] pvi = null;

		//	IEnumerable GetArray<T>(FBXNode node, string name) {

		//		if (node[name] == null) {
		//			node = node[$"LayerElement{name}"];
		//		}
		//		if (node == null)
		//			yield break;

		//		var data = node[name];
		//		var indices = node[$"{name}Index"];
		//		if (data == null)
		//			data = node[$"{name}s"];
		//		if (indices == null)
		//			indices = node[$"{name}sIndex"];
		//		if (data == null || indices == null)
		//			yield break;

		//		if (typeof(T) == typeof(Vector2) || typeof(T) == typeof(Vector3) ||typeof(T) == typeof(Vector4)) {
		//			float[] fArray;
		//			if (data.properties[0] is double[]) {
		//				fArray = ((double[])data.properties[0]).Select(x => (float)x).ToArray();
		//			}
		//			else if (data.properties[0] is long[]) {
		//				fArray = ((long[])data.properties[0]).Select(x => (float)x).ToArray();
		//			}
		//			else if (data.properties[0] is int[]) {
		//				fArray = ((int[])data.properties[0]).Select(x => (float)x).ToArray();
		//			}
		//			else {
		//				fArray = (float[])data.properties[0];
		//			}

		//			int[] iArray = (int[])indices.properties[0];

		//			if (node["MappingInformationType"].properties[0].ToString() == "ByVertice") {
		//				int[] newArray = new int[pvi.Length];

		//				for (int i = 0; i < pvi.Length; i++) {
		//					int v = pvi[i];
		//					if (v < 0)
		//						v = ~v;
		//					newArray[i] = iArray[v];
		//				}

		//				iArray = newArray;
		//			}

		//			if (typeof(T) == typeof(Vector2)) {
		//				for (int i = 0; i < iArray.Length; i++) {
		//					int idx = iArray[i] * 2;
		//					yield return new Vector2(fArray[idx], fArray[idx + 1]);
		//				}
		//			}
		//			else if (typeof(T) == typeof(Vector3)) {
		//				for (int i = 0; i < iArray.Length; i++) {
		//					int idx = iArray[i] * 3;
		//					yield return new Vector3(fArray[idx], fArray[idx + 1], fArray[idx + 2]);
		//				}
		//			}
		//			else if (typeof(T) == typeof(Vector4)) {
		//				for (int i = 0; i < iArray.Length; i++) {
		//					int idx = iArray[i] * 4;
		//					yield return new Vector4(fArray[idx], fArray[idx + 1], fArray[idx + 2], fArray[idx + 3]);
		//				}
		//			}
		//		}


		//		yield break;
		//	}


		//	var objects = node["Objects"];

		//	Dictionary<string, MonocleModel> retval = new Dictionary<string, MonocleModel>();
		//	List<(MonocleVertex[] verts, int[][] indices)> meshes = new List<(MonocleVertex[], int[][])>();
		//	int meshIndex = 0;


		//	foreach (var child in objects.children) {
		//		switch (child.name) {
		//			case "Geometry": {

		//				var dArray = child["Vertices"].properties[0] as double[];
		//				Vector3[] verts = new Vector3[dArray.Length / 3];
		//				for (int i = 0; i < dArray.Length; i += 3) {
		//					verts[i / 3] = new Vector3((float)dArray[i], (float)dArray[i + 2], -(float)dArray[i + 1]);
		//				}

		//				List<Vector3> normals = new List<Vector3>();

		//				pvi = child["PolygonVertexIndex"].properties[0] as int[];

		//				MonocleVertex[] data = new MonocleVertex[pvi.Length];

		//				List<int> faceCount = new List<int>();
		//				int c = 0;

		//				for (int i = 0; i < data.Length; i++) {
		//					c++;
		//					int index = pvi[i];
		//					if (index < 0) {
		//						index = ~index;
		//						faceCount.Add(c);
		//						c = 0;
		//					}
		//					data[i].Position = verts[index];
		//				}

		//				int idx = 0;
		//				if (child[$"LayerElementColor"] == null) {

		//					for (int i = 0; i < data.Length; i++) {
		//						data[i].Color = Vector4.One;
		//					}
		//				}
		//				else {
		//					foreach (Vector4 item in GetArray<Vector4>(child, "Color")) {
		//						data[idx++].Color = item;
		//					}
		//				}
		//				idx = 0;
		//				foreach (Vector2 item in GetArray<Vector2>(child, "UV")) {
		//					data[idx++].TextureCoordinate = new Vector2(item.X, 1 - item.Y);
		//				}
		//				idx = 0;
		//				foreach (Vector3 item in GetArray<Vector3>(child, "Normal")) {
		//					data[idx++].Normal = new Vector3(item.X, item.Z, -item.Y);
		//				}

		//				List<MonocleVertex> comp = new List<MonocleVertex>(data.Distinct());
		//				List<List<int>> finalIndices = new List<List<int>>();

		//				string asdf = "AllSame";
		//				if (child.HasChild("LayerElementMaterial")) {
		//					asdf = child["LayerElementMaterial"]["MappingInformationType"].properties[0].ToString();
		//				}
		//				switch (asdf) {
		//					default:
		//					case "AllSame": {

		//						List<int> indices = new List<int>();
		//						idx = 0;
		//						for (int i = 0; i < faceCount.Count; i++) {
		//							for (int j = 0; j < faceCount[i] - 2; j++) {
		//								indices.Add(comp.IndexOf(data[idx]));
		//								indices.Add(comp.IndexOf(data[idx + j + 2]));
		//								indices.Add(comp.IndexOf(data[idx + j + 1]));
		//							}
		//							idx += faceCount[i];
		//						}
		//						finalIndices.Add(indices);
		//						break;
		//					}
		//					case "ByPolygon": {
		//						var exChild = child["LayerElementMaterial"];

		//						idx = 0;


		//						int[] matArray = exChild["Materials"].properties[0] as int[];

		//						for (int i = 0; i < faceCount.Count; i++) {
		//							int index = matArray[i];
		//							while (finalIndices.Count <= index) {
		//								finalIndices.Add(new List<int>());
		//							}
		//							for (int j = 0; j < faceCount[i] - 2; j++) {
		//								finalIndices[index].Add(comp.IndexOf(data[idx]));
		//								finalIndices[index].Add(comp.IndexOf(data[idx + j + 2]));
		//								finalIndices[index].Add(comp.IndexOf(data[idx + j + 1]));
		//							}
		//							idx += faceCount[i];
		//						}
		//						break;
		//					}
		//				}



		//				meshes.Add((comp.ToArray(), finalIndices.Select((l) => { return l.ToArray(); }).ToArray()));

		//				break;
		//			}
		//			case "Model": {

		//				if (child.properties[2].ToString() == "Mesh") {

		//					retval[child.properties[1].ToString().Split("\0\u0001")[0]] = new MonocleModel(meshes[meshIndex].verts, meshes[meshIndex].indices);
		//					retval[child.properties[1].ToString().Split("\0\u0001")[0]].CalculateTangent();

		//					meshIndex++;
		//				}
		//				break;
		//			}
		//		}
		//	}

		//	return retval;
		//}
		public static Dictionary<string, MonocleModel> Import(string contentPath) {

			var ext = Path.GetExtension(contentPath);
			var asset = AssetLoader.GetContent(contentPath);

			if (asset == null)
				return null;

			switch (ext) {
				case ".obj": {

					List<Vector3> verts = new List<Vector3>();
					List<Vector3> normals = new List<Vector3>();
					List<Vector2> uvs = new List<Vector2>();

					List<MonocleVertex> facesReal = new List<MonocleVertex>();


					Dictionary<string, MonocleModel> retval = new Dictionary<string, MonocleModel>();
					string current = null;

					int count = 1;

					void AddObject() {
						List<Vector3> faceVerts = new List<Vector3>();
						List<Vector3> faceNormals = new List<Vector3>();
						List<Vector2> faceUVs = new List<Vector2>();
						List<int> faces = new List<int>();

						var dist = new List<MonocleVertex>(facesReal.Distinct());

						for (int i = 0; i < facesReal.Count; i++) {

							var val = facesReal[i];
							short index = (short)dist.IndexOf(val);

							faces.Add(index);
						}
						for (int i = 0; i < dist.Count; i++) {
							faceVerts.Add(dist[i].Position);
							faceNormals.Add(dist[i].Normal);
							faceUVs.Add(dist[i].TextureCoordinate);
						}
						var model = new MonocleModel(faceVerts.ToArray(), faceNormals.ToArray(), faceUVs.ToArray(), faces.ToArray());
						model.CalculateTangent();
						retval.Add(current, model);

					}

					using (StreamReader reader = new StreamReader(asset.ContentStream)) {
						while (!reader.EndOfStream) {
							string line = reader.ReadLine();
							if (line.StartsWith("#"))
								continue;


							string[] split = line.Split(' ');

							switch (split[0]) {
								case "o":
									if (current != null) {

										AddObject();
									}

									facesReal.Clear();

									current = split[1];
									break;
								case "v": {
									var group = Regex.Matches(line, @"[-\d\.]+");
									verts.Add(new Vector3(float.Parse(group[0].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(group[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(group[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
									break;
								}
								case "vn": {
									var group = Regex.Matches(line, @"[-\d\.]+");
									normals.Add(new Vector3(float.Parse(group[0].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(group[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(group[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
									break;
								}
								case "vt": {
									var group = Regex.Matches(line, @"[-\d\.]+");
									uvs.Add(new Vector2(float.Parse(group[0].Value, System.Globalization.CultureInfo.InvariantCulture), 1 - float.Parse(group[1].Value, System.Globalization.CultureInfo.InvariantCulture)));
									break;
								}
								case "f": {
									var group = Regex.Match(line, @"f (\d+)/(\d+)/(\d+) (\d+)/(\d+)/(\d+) (\d+)/(\d+)/(\d+)");


									facesReal.Add(new MonocleVertex(
										verts[int.Parse(group.Groups[1].Value) - 1],
										normals[int.Parse(group.Groups[3].Value) - 1],
										Vector3.Zero, Vector3.Zero,
										uvs[int.Parse(group.Groups[2].Value) - 1], Vector4.One));
									facesReal.Add(new MonocleVertex(
										verts[int.Parse(group.Groups[7].Value) - 1],
										normals[int.Parse(group.Groups[9].Value) - 1],
										Vector3.Zero, Vector3.Zero,
										uvs[int.Parse(group.Groups[8].Value) - 1], Vector4.One));
									facesReal.Add(new MonocleVertex(
										verts[int.Parse(group.Groups[4].Value) - 1],
										normals[int.Parse(group.Groups[6].Value) - 1],
										Vector3.Zero, Vector3.Zero,
										uvs[int.Parse(group.Groups[5].Value) - 1], Vector4.One));

									break;
								}
							}
						}
					}

					AddObject();

					return retval;
				}
				//case ".fbx": {

				//	return ImportMeshes(FBXNode.GetFBX(contentPath));

				//}
			}

			return null;
		}



		public int MaterialCount => indices.Length;

		internal MonocleVertex[] vertices;
		internal short[][] indices;
		internal Dictionary<string, float[]> weights, balanced;

		public MonocleModel(MonocleVertex[] vertices) {
			this.vertices = vertices;
			indices = new short[1][];
			indices[0] = new short[indices[0].Length];
			for (int i = 0; i < vertices.Length; i++) {
				indices[0][i] = (short)i;
			}
		}
		public MonocleModel(MonocleVertex[] vertices, int[] indices) {
			this.vertices = vertices;
			this.indices = new short[1][];
			this.indices[0] = new short[indices.Length];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[0][i] = (short)indices[i];
			}
		}
		public MonocleModel(MonocleVertex[] vertices, int[][] indices) {
			this.vertices = vertices;
			this.indices = new short[indices.Length][];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[i] = new short[indices[i].Length];
				for (int j = 0; j < indices[i].Length; j++) {
					this.indices[i][j] = (short)indices[i][j];
				}
			}
		}
		public MonocleModel(MonocleVertex[] vertices, int[][] indices, Dictionary<string, float[]> weights) {
			this.weights = weights;
			this.vertices = vertices;
			this.indices = new short[indices.Length][];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[i] = new short[indices[i].Length];
				for (int j = 0; j < indices[i].Length; j++) {
					this.indices[i][j] = (short)indices[i][j];
				}
			}
			balanced = new Dictionary<string, float[]>();

			if (weights.Count > 0) {
				float[] total = new float[vertices.Length];

				foreach (var value in weights.Values) {
					for (int i = 0; i < value.Length; i++) {
						total[i] += value[i];
					}
				}
				foreach (var key in weights.Keys) {
					float[] value = new float[total.Length];
					for (int i = 0; i < total.Length; i++) {
						if (total[i] == 0)
							value[i] = 0;
						else
							value[i] = weights[key][i] / total[i];
					}
					balanced.Add(key, value);
				}

			}

		}
		public MonocleModel(Vector3[] points, int[] indices) {

			vertices = new MonocleVertex[points.Length];
			for (int i = 0; i < points.Length; i++) {
				vertices[i].Position = points[i];
				vertices[i].TextureCoordinate = Vector2.Zero;
				vertices[i].Color = Color.White.ToVector4();
			}

			this.indices = new short[1][];
			this.indices[0] = new short[indices.Length];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[0][i] = (short)indices[i];
			}
		}
		public MonocleModel(Vector3[] points, Vector2[] uvs, int[] indices) {
			if (points.Length != uvs.Length)
				throw new FormatException();

			vertices = new MonocleVertex[points.Length];
			for (int i = 0; i < points.Length; i++) {
				vertices[i].Position = points[i];
				vertices[i].TextureCoordinate = uvs[i];
				vertices[i].Color = Color.White.ToVector4();
			}
			this.indices = new short[1][];
			this.indices[0] = new short[indices.Length];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[0][i] = (short)indices[i];
			}
			RecalculateNormals();
		}
		public MonocleModel(Vector3[] points, Vector3[] normals, Vector2[] uvs, int[] indices) {
			if (points.Length != normals.Length || points.Length != uvs.Length)
				throw new FormatException();

			vertices = new MonocleVertex[points.Length];
			for (int i = 0; i < points.Length; i++) {
				vertices[i].Position = points[i];
				vertices[i].Normal = normals[i];
				vertices[i].TextureCoordinate = uvs[i];
				vertices[i].Color = Color.White.ToVector4();
			}
			this.indices = new short[1][];
			this.indices[0] = new short[indices.Length];
			for (int i = 0; i < indices.Length; i++) {
				this.indices[0][i] = (short)indices[i];
			}
			CalculateTangent();
		}

		public void RecalculateNormals() {
			RecalculateNormals(vertices, indices);
		}
		public void CalculateTangent() {
			CalculateTangent(vertices, indices);
		}

		public float GetWeight(string tag, int index) {
			if (weights.ContainsKey(tag))
				return weights[tag][index];
			return 0;
		}
		public float GetBalancedWeight(string tag, int index) {
			if (balanced.ContainsKey(tag))
				return balanced[tag][index];
			return 0;
		}
	}
}
