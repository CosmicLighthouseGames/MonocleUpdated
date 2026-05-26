using FMOD;
using Isometric_Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core.Events;

namespace Monocle {

	public class GenericHeap {
		public struct HeapRange {
			public int Start, End;
			public int Length => End - Start;

			public HeapRange(int start, int end) {
				Start = start;
				End = end;
			}
			public HeapRange MoveStart(int offset) {
				Start += offset;
				return this;
			}
			public HeapRange MoveEnd(int offset) {
				End += offset;
				return this;
			}

			public override string ToString() {
				return $"{Start} -- {End}";
			}
		}
		public int MaxSize { get; private set; }
		public List<HeapRange> ranges, gaps;

		public int GetUsedSpace() {
			int l = 0;
			foreach (var r in ranges) {
				l += r.Length;
			}
			return l;
		}

		public GenericHeap(int maxLength) {
			MaxSize = maxLength;
			ranges = new List<HeapRange>();
			gaps = new List<HeapRange>();
			gaps.Add(new HeapRange(0, maxLength));
		}


		public bool HasSpace(int length) {
			if (length > MaxSize) {
				return false;
			}
			for (int i = 0; i < gaps.Count; i++) {
				if (length <= gaps[i].Length) {
					return true;
				}
			}
			return false;
		}
		public int AddRange(int length) {
			if (length > MaxSize) {
				return -1;
			}
			int bestLocation = -1, bestIndex = -1, gapSize = 0;
			for (int i = 0; i < gaps.Count; i++) {
				if (length <= gaps[i].Length) {
					if (bestLocation == -1 || gaps[i].Length < gapSize) {
						bestIndex = i;
						bestLocation = gaps[i].Start;
						gapSize = gaps[i].Length;
					}
				}
			}
			if (bestLocation == -1) {
				return -1;
			}

			HeapRange range = new HeapRange(bestLocation, bestLocation + length);

			gaps[bestIndex] = gaps[bestIndex].MoveStart(length);
			if (gaps[bestIndex].Length == 0) {
				gaps.RemoveAt(bestIndex);
			}

			if (ranges.Count == 0) {
				ranges.Add(range);
			}
			else {
				if (ranges[0].Start > range.End) {
					ranges.Insert(0, range);
				}
				else {
					for (int i = 0; i < ranges.Count; i++) {
						if (ranges[i].End >= range.Start) {
							ranges.Insert(i + 1, range);
							break;
						}
					}
				}
			}
			return range.Start;
		}
		public void RemoveRange(int start) {
			for (int i = 0; i < ranges.Count; i++) {
				if (ranges[i].Start == start) {
					var range = ranges[i];

					ranges.RemoveAt(i);

					for (i = 0; i < gaps.Count; i++) {
						if (gaps[i].Start == range.End) {
							gaps[i] = gaps[i].MoveStart(-range.Length);
							if (i > 0 && gaps[i - 1].End == gaps[i].Start) {
								gaps[i - 1] = gaps[i - 1].MoveEnd(gaps[i].Length);
								gaps.RemoveAt(i);
							}
							return;
						}
						if (gaps[i].End == range.Start) {
							gaps[i] = gaps[i].MoveEnd(range.Length);
							if (i < gaps.Count - 1 && gaps[i + 1].Start == gaps[i].End) {
								gaps[i + 1] = gaps[i + 1].MoveStart(-gaps[i].Length);
								gaps.RemoveAt(i);
							}
							return;
						}
					}
					if (i == gaps.Count) {
						if (range.End < gaps[0].Start) {
							gaps.Insert(0, range);
						}
						else {
							for (i = gaps.Count - 1; i >= 0; i--) {
								if (range.Start >= gaps[i].End) {
									gaps.Insert(i + 1, range);
									break;
								}
							}
						}
					}


				}
			}
		}
	}
	public static class MeshHeap {

		static List<(DynamicVertexBuffer buffer, GenericHeap heap)> vertices;
		static List<(DynamicVertexBuffer buffer, GenericHeap heap)> weights;
		static List<(DynamicIndexBuffer buffer, GenericHeap heap)> indices;
		static GraphicsDevice graphics;

		public static int DataUsed {
			get {
				int l = 0;
				foreach (var buffer in vertices) {
					l += buffer.heap.GetUsedSpace();
				}
				foreach (var buffer in indices) {
					l += buffer.heap.GetUsedSpace();
				}
				return l;
			}
		}

		public static void Initialize(GraphicsDevice device) {
			graphics = device;

			vertices = new List<(DynamicVertexBuffer, GenericHeap)>();
			weights = new List<(DynamicVertexBuffer, GenericHeap)>();
			indices = new List<(DynamicIndexBuffer, GenericHeap)>();
			AddVertexBuffer();
			AddWeightBuffer();
            AddIndexBuffer();
            AddVertexBuffer();
            AddWeightBuffer();
            AddIndexBuffer();
            AddVertexBuffer();
            AddWeightBuffer();
            AddIndexBuffer();

        }

		static void AddVertexBuffer() {
			Engine.WaitForRendering();
			vertices.Add((new DynamicVertexBuffer(graphics, typeof(MonocleVertex), 0x20000, BufferUsage.WriteOnly), new GenericHeap(0x20000)));
		}
		static void AddWeightBuffer() {
			Engine.WaitForRendering();
			weights.Add((new DynamicVertexBuffer(graphics, typeof(MonocleVertexWeight), 0x20000, BufferUsage.WriteOnly), new GenericHeap(0x20000)));
		}
		static void AddIndexBuffer() {
			Engine.WaitForRendering();
			indices.Add((new DynamicIndexBuffer(graphics, IndexElementSize.SixteenBits, 0xC0000, BufferUsage.WriteOnly), new GenericHeap(0xC0000)));
		}

		static (VertexBuffer, int) GetVBuffer(MonocleVertex[] data) {
			int index = vertices.Count;
			for (int i = 0; i < vertices.Count; i++) {
				if (vertices[i].heap.HasSpace(data.Length)) {
					index = i;
					break;
				}
			}
			if (index == vertices.Count)
				AddVertexBuffer();

			var buffer = (vertices[index].buffer, vertices[index].heap.AddRange(data.Length));
			buffer.Item1.SetData(buffer.Item2 * MonocleVertex.VertexDeclaration.VertexStride, data, 0, data.Length, MonocleVertex.VertexDeclaration.VertexStride);

			return buffer;
        }
        static VertexBuffer GetWeightBuffer(MonocleVertexWeight[] data)
        {
            int index = weights.Count;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].heap.HasSpace(data.Length))
                {
                    index = i;
					break;
                }
            }
            if (index == weights.Count)
                AddWeightBuffer();

			int size = weights[index].heap.AddRange(data.Length);
            var buffer = weights[index].buffer;
            buffer.SetData(size * MonocleVertexWeight.VertexDeclaration.VertexStride, data, 0, data.Length, MonocleVertexWeight.VertexDeclaration.VertexStride);

            return buffer;
        }
        static (IndexBuffer, int) GetIBuffer(short[] inds, int offset) {
			int index = indices.Count;
			for (int i = 0; i < indices.Count; i++) {
				if (indices[i].heap.HasSpace(inds.Length)) {
					index = i;
					break;
				}
			}
			if (index == indices.Count)
				AddIndexBuffer();

			inds = Array.ConvertAll(inds, value => (short)(value + 0));

			var buffer = (indices[index].buffer, indices[index].heap.AddRange(inds.Length));
			buffer.Item1.SetData(buffer.Item2 * 2, inds, 0, inds.Length);

			return buffer;
		}

		public static MeshPointer CreateSection(MonocleVertex[] verts, short[] inds) {


			try {
				var vb = GetVBuffer(verts);
				var ib = GetIBuffer(inds, vb.Item2);


				return new MeshPointer(graphics, vb.Item1, ib.Item1, vb.Item2, ib.Item2, inds.Length / 3);

			}
			catch {
				return null;
			}
		}
		public static MeshPointer[] CreateSections(MonocleVertex[] verts, short[][] inds) {


			try {
				MeshPointer[] pointers = new MeshPointer[inds.Length];

				var vb = GetVBuffer(verts);

				for (int i = 0; i < inds.Length; i++) {
					var ib = GetIBuffer(inds[i], vb.Item2);

					pointers[i] = new MeshPointer(graphics, vb.Item1, ib.Item1, vb.Item2, ib.Item2, inds[i].Length / 3);

				}

				return pointers;
			}
			catch {
				return null;
			}
        }
        public static VertexBuffer CreateWeight(MonocleVertexWeight[] weights)
        {
            try
            {
                return GetWeightBuffer(weights);
            }
            catch
            {
                return null;
            }
        }
        internal static void DisposePointer(MeshPointer pointer) {
			foreach (var b in vertices) {
				if (pointer.VertexBuffer == b.buffer) {
					b.heap.RemoveRange(pointer.VertexOffset);
					break;
				}
			}
			foreach (var b in indices) {
				if (pointer.IndexBuffer == b.buffer) {
					b.heap.RemoveRange(pointer.IndexOffset);
					break;
				}
			}
		}

		public static void Check() {

		}
	}
	public class MeshPointer : IDisposable {
		GraphicsDevice gd;

		public VertexBuffer VertexBuffer { get; internal set; }
		public IndexBuffer IndexBuffer { get; internal set; }
		public int VertexOffset { get; internal set; }
		public int IndexOffset { get; internal set; }
		public int PrimitiveCount { get; internal set; }

		public MeshPointer(GraphicsDevice graphicsDevice, VertexBuffer vbuffer, IndexBuffer ibuffer, int voffset, int ioffset, int count) {
			gd = graphicsDevice;
			VertexBuffer = vbuffer;
			IndexBuffer = ibuffer;
			VertexOffset = voffset;
			IndexOffset = ioffset;
			PrimitiveCount = count;
		}

		public void SetIndex() {
			gd.SetVertexBuffer(VertexBuffer);
			gd.Indices = IndexBuffer;
		}
		public void SetIndex(VertexBuffer extra) {
			if (extra == null)
			{
				SetIndex();
				return;

            }
			gd.SetVertexBuffers(new VertexBufferBinding(VertexBuffer, 0), new VertexBufferBinding(extra, 0));
			gd.Indices = IndexBuffer;
        }
        public void RenderList()
        {
            if (PrimitiveCount <= 0)
                return;

            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, VertexOffset, IndexOffset, PrimitiveCount);
        }
        public void Render() {
			if (PrimitiveCount <= 0)
				return;

            SetIndex();
			RenderList();
        }

		public void Dispose() {
			MeshHeap.DisposePointer(this);
		}
	}

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

			mat = LocalTransform;//Matrix.CreateScale(LocalScale) * Matrix.CreateFromQuaternion(localLocalRotation);
			
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

		internal MonocleBone CreateCopy() {
			return new MonocleBone(Name) {
				Position_orig = Position_orig,
				Rotation_orig = Rotation_orig
			};
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

		public MonocleArmature CreateCopy() {

			var newArm = new MonocleArmature();

			foreach (var key in bones.Keys) {
				var bone = bones[key].CreateCopy();
				newArm.AddBone(bone);
			}
			foreach (var key in bones.Keys) {
				var old = GetBone(key);
				if (old.Parent != null) {
					newArm.GetBone(old.Parent.Name).AddChild(newArm.GetBone(key));
				}
			}

			return newArm;
		}
	}

	public class FBXScene {
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

		struct VertexData
		{
			public MonocleVertex vertex;
			public int VertexIndex;
			public string WeightA, WeightB, WeightC, WeightD;
		}
		struct BonePairs
		{
			public string A, B, C, D;

			public static BonePairs Get(Dictionary<string, float> dict)
			{
				var values = dict.OrderBy((a) => { return -a.Value; }).ToArray();

				string[] names = new string[4];
				Array.Copy(values.Select((a) => a.Key).ToArray(), names, Math.Min(values.Length, 4));
				names = names.OrderBy(x => x == null).ThenBy(x => x).ToArray();

				var val = new BonePairs();

				val.A = names[0];
				val.B = names[1];
				val.C = names[2];
				val.D = names[3];

				return val;
			}
            public static BonePairs Get(VertexData data)
            {
                var val = new BonePairs();

                val.A = data.WeightA;
                val.B = data.WeightB;
                val.C = data.WeightC;
                val.D = data.WeightD;

                return val;
            }
        }

		internal static FBXScene Test()
		{
			FBXScene retval = new FBXScene();


			var armature = new MonocleArmature();
			var boneTop = new MonocleBone("Top");
			var boneBottom = new MonocleBone("Bottom");
			//boneBottom.AddChild(boneTop);

			armature.AddBone(boneTop);
			armature.AddBone(boneBottom);

			retval.armatures["Armature"] = armature;

			VertexData[] data = new VertexData[]
			{
				new VertexData() {vertex = new MonocleVertex(){Position = new Vector3(-1, -1, 0), Color = Vector4.One} },
                new VertexData() {vertex = new MonocleVertex(){Position = new Vector3(-1,  1, 0), Color = Vector4.One} },
                new VertexData() {vertex = new MonocleVertex(){Position = new Vector3( 1,  1, 0), Color = Vector4.One} },
                new VertexData() {vertex = new MonocleVertex(){Position = new Vector3( 1, -1, 0), Color = Vector4.One} },
            };

			short[] indices = new short[]
			{
				0, 1, 2,
				0, 2, 3,
			};

			MonocleVertexWeight[] weigh = new MonocleVertexWeight[]
			{
                new MonocleVertexWeight() { Weight0 = 0, Weight1 = 0, Weight2 = 0, Weight3 = 1},
            };

			var pointers = MeshHeap.CreateSection(data.Select(x => x.vertex).ToArray(), indices);
			var weights = MeshHeap.CreateWeight(weigh);

			MonocleModelPart part = new MonocleModelPart(pointers, weights, "Top", "Bottom", null, null);

			retval.parts.Add("Tester", new MonocleModelPart[][]
			{
				new MonocleModelPart[]{ part }
			});

			return retval;
        }
		public static FBXScene Import(LoadedAsset content) {

			FBXScene retval = new FBXScene();


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

			Dictionary<long, FBXNode> nodeIDs = new Dictionary<long, FBXNode>();


			Dictionary<long, List<long>> parent2Child = new Dictionary<long, List<long>>();
			Dictionary<long, List<long>> child2Parent = new Dictionary<long, List<long>>();

			List<FBXNode> meshes = new List<FBXNode>();
			List<FBXNode> armatures = new List<FBXNode>();
			IEnumerable<(string, float[])> getWeights(int vertLength, FBXNode parentNode) {


				if (!parent2Child.ContainsKey((long)parentNode.properties[0]))
					yield break;

				foreach (var val in parent2Child[(long)parentNode.properties[0]]) {
					var node = nodeIDs[val];

					if (node.name == "Model" || node.properties[2] as string == "LimbNode") {
						foreach (var weight in getWeights(vertLength, node)) {
							yield return weight;
						}
					}
					if (node.name != "Deformer" || node.properties[2] as string != "Cluster")
						continue;

					if (node["Indexes"] == null)
						continue;


					var indices = node["Indexes"].properties[0] as int[];
					var weights = node["Weights"].properties[0] as double[];

					float[] values = new float[vertLength];

					for (int i = 0; i < indices.Length; i++)
                    {
						values[indices[i]] = (float)weights[i];
					}

					yield return ((node.properties[1] as string).Split("\0\u0001")[0], values);
					foreach (var weight in getWeights(vertLength, node)) {
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
					case "Geometry":
						{

							var dArray = child["Vertices"].properties[0] as double[];
							VertexData[] verts = new VertexData[dArray.Length / 3];
							for (int i = 0; i < dArray.Length; i += 3)
							{
								verts[i / 3].vertex.Position = new Vector3((float)dArray[i], (float)dArray[i + 2], -(float)dArray[i + 1]);
								verts[i / 3].VertexIndex = i / 3;
							}

                            Dictionary<string, float[]> weights = new Dictionary<string, float[]>();
                            if (parent2Child.ContainsKey(uuid))
                            {
                                foreach (var val in parent2Child[uuid])
                                {
                                    var node = nodeIDs[val];
                                    if (node.name == "Deformer" && node.properties[2] as string == "Skin")
                                    {
                                        foreach (var value in getWeights(verts.Length, node))
                                        {
                                            weights.Add(value.Item1, value.Item2);

                                        }
                                    }
                                }
                            }

							pvi = child["PolygonVertexIndex"].properties[0] as int[];


                            Dictionary<BonePairs, List<int>> segments = new Dictionary<BonePairs, List<int>>();

                            for (int i = 0; i < pvi.Length; i += 2)
                            {
                                int offset = 0;

                                do
                                {
                                    int last = pvi[i + 2 + offset];
                                    if (last < 0)
                                        last = ~last;


                                    Dictionary<string, float> values = new Dictionary<string, float>();

                                    foreach (var weight in weights)
                                    {
                                        values[weight.Key] = weight.Value[verts[pvi[i]].VertexIndex];
                                        values[weight.Key] += weight.Value[verts[pvi[i + offset + 1]].VertexIndex];
                                        values[weight.Key] += weight.Value[verts[last].VertexIndex];
                                    }

                                    var pairs = BonePairs.Get(values);
                                    if (!segments.ContainsKey(pairs))
                                    {
                                        segments[pairs] = new List<int>();
                                    }
                                    segments[pairs].Add(i);

                                    verts[pvi[i]].WeightA = pairs.A;
                                    verts[pvi[i]].WeightB = pairs.B;
                                    verts[pvi[i]].WeightC = pairs.C;
                                    verts[pvi[i]].WeightD = pairs.D;

                                    verts[pvi[i + offset + 1]].WeightA = pairs.A;
                                    verts[pvi[i + offset + 1]].WeightB = pairs.B;
                                    verts[pvi[i + offset + 1]].WeightC = pairs.C;
                                    verts[pvi[i + offset + 1]].WeightD = pairs.D;

                                    verts[last].WeightA = pairs.A;
                                    verts[last].WeightB = pairs.B;
                                    verts[last].WeightC = pairs.C;
                                    verts[last].WeightD = pairs.D;

                                    offset++;
                                }
                                while (pvi[i + offset + 1] >= 0);
                                i += offset;
                            }

                            VertexData[] data = new VertexData[pvi.Length];
							

							// Triangulating faces
							List<int> faceCount = new List<int>();
							int c = 0;
							for (int i = 0; i < data.Length; i++)
							{
								c++;
								int index = pvi[i];
								if (index < 0)
								{
									index = ~index;
									faceCount.Add(c);
									c = 0;
								}
								data[i] = verts[index];
							}

							// Other properties
							int idx = 0;
							if (child[$"LayerElementColor"] == null)
							{

								for (int i = 0; i < data.Length; i++)
								{
									data[i].vertex.Color = Vector4.One;
								}
							}
							else
							{
								foreach (Vector4 item in GetArray<Vector4>(child, "Color"))
								{
									data[idx++].vertex.Color = item;
								}
							}
							idx = 0;
							foreach (Vector2 item in GetArray<Vector2>(child, "UV"))
							{
								data[idx++].vertex.TextureCoordinate = new Vector2(item.X, 1 - item.Y);
							}
							idx = 0;
							foreach (Vector3 item in GetArray<Vector3>(child, "Normal"))
							{
								data[idx++].vertex.Normal = new Vector3(item.X, item.Z, -item.Y);
							}

							List<List<int>> finalIndices = new List<List<int>>();
							Dictionary<VertexData, int> comp = new Dictionary<VertexData, int>();



							for (int i = 0; i < data.Length; i++)
							{
								var vert = data[i];

								if (!comp.ContainsKey(vert))
								{
									comp.Add(vert, comp.Count);
								}
							}


							// Material Mapping
							string mappingType = "AllSame";
							if (child.HasChild("LayerElementMaterial"))
							{
								mappingType = child["LayerElementMaterial"]["MappingInformationType"].properties[0].ToString();
							}
							switch (mappingType)
							{
								default:
								case "AllSame":
									{

										List<int> indices = new List<int>();
										idx = 0;
										for (int i = 0; i < faceCount.Count; i++)
										{
											for (int j = 0; j < faceCount[i] - 2; j++)
                                            {
                                                indices.Add(comp[data[idx]]);
                                                indices.Add(comp[data[idx + j + 2]]);
                                                indices.Add(comp[data[idx + j + 1]]);
                                            }
											idx += faceCount[i];
										}
										finalIndices.Add(indices);
										break;
									}
								case "ByPolygon":
									{
										var exChild = child["LayerElementMaterial"];

										idx = 0;


										int[] matArray = exChild["Materials"].properties[0] as int[];

										for (int i = 0; i < faceCount.Count; i++)
										{
											int index = matArray[i];
											while (finalIndices.Count <= index)
											{
												finalIndices.Add(new List<int>());
											}
											for (int j = 0; j < faceCount[i] - 2; j++)
											{
												finalIndices[index].Add(comp[data[idx]]);
												finalIndices[index].Add(comp[data[idx + j + 2]]);
												finalIndices[index].Add(comp[data[idx + j + 1]]);
											}
											idx += faceCount[i];
										}
										break;
									}
							}

							List<object> retval = new List<object>(){
								comp.Keys.ToArray(),
								finalIndices.Select((l) => { return l.ToArray(); }).ToArray(),
								weights
							};


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
						if (meshData != null)
							break;
					}
				}

				var verts = (VertexData[])meshData[0];
				var indices = Array.ConvertAll((int[][])meshData[1], input => Array.ConvertAll(input, input2 => (short)input2));

                CalculateTangent(verts.Select(x => x.vertex).ToArray(), indices);

				var weightDict = meshData[2] as Dictionary<string, float[]>;

				List<short[]> totalCompiled = new List<short[]>();

				List<(int, BonePairs)> totalIndices = new List<(int, BonePairs)>();
				int totalI = 0;

				foreach (var inds in indices)
				{
                    Dictionary<BonePairs, List<int>> segments = new Dictionary<BonePairs, List<int>>();

                    for (int i = 0; i < inds.Length; i += 3)
					{
						var pairs = BonePairs.Get(verts[inds[i]]);
						if (!segments.ContainsKey(pairs))
						{
							segments[pairs] = new List<int>();
						}
						segments[pairs].Add(i);
					}

					List<short[]> compiled = new List<short[]>();

					foreach (var item in segments)
					{
						List<short> indicesCompiled = new List<short>();

						foreach (var i in item.Value)
						{
							for (int j = 0; j < 3; j++)
							{
								indicesCompiled.Add(inds[i + j]);
							}
						}
						totalCompiled.Add(indicesCompiled.ToArray());
						totalIndices.Add((totalI, item.Key));
					}

					totalI++;
                }


				totalI = 0;

				List<MonocleModelPart[]> modelWhole = new List<MonocleModelPart[]>();
				List<MonocleModelPart> parts = new List<MonocleModelPart>();

				int index = 0;
				for (int i = 0; i < totalIndices.Count; i++)
                {
                    var p = totalIndices[i];

					if (p.Item1 != totalI)
					{
						totalI = p.Item1;
						modelWhole.Add(parts.ToArray());
						parts.Clear();
					}
					MonocleVertex[] vertices = new MonocleVertex[totalCompiled[i].Length];
					short[] inds = new short[vertices.Length];
					MonocleVertexWeight[] weights = new MonocleVertexWeight[vertices.Length];

					bool hasWeight = false;

					for (int j = 0; j < vertices.Length; j++)
					{
						var vert = verts[totalCompiled[i][j]];

						vertices[j] = vert.vertex;
						inds[j] = (short)j;

						weights[j] = new MonocleVertexWeight()
						{
							Weight0 = vert.WeightA == null ? 0 : weightDict[vert.WeightA][vert.VertexIndex],
							Weight1 = vert.WeightB == null ? 0 : weightDict[vert.WeightB][vert.VertexIndex],
							Weight2 = vert.WeightC == null ? 0 : weightDict[vert.WeightC][vert.VertexIndex],
							Weight3 = vert.WeightD == null ? 0 : weightDict[vert.WeightD][vert.VertexIndex],
						};

						if (!hasWeight && (weights[j].Weight0 != 0 || weights[j].Weight1!= 0 || weights[j].Weight2 != 0 || weights[j].Weight3 != 0))
                        {
							hasWeight = true;
						}
					}

                    var pointers = MeshHeap.CreateSection(vertices.ToArray(), inds);

					VertexBuffer weightBuffer = null;

					if (hasWeight)
					{
						weightBuffer = MeshHeap.CreateWeight(weights);

                    }

                    parts.Add(new MonocleModelPart(pointers, weightBuffer, p.Item2.A, p.Item2.B, p.Item2.C, p.Item2.D));

					index++;
                }
                modelWhole.Add(parts.ToArray());
                retval.parts[split[0]] = modelWhole.ToArray();
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

			return retval;
		}

		Dictionary<string, MonocleModelPart[][]> parts = new Dictionary<string, MonocleModelPart[][]>();

		Dictionary<string, MonocleArmature> armatures = new Dictionary<string, MonocleArmature>();

		FBXScene() {

		}

		public MonocleModel GetMesh(string mesh) {

			List<MonocleModelMaterialSlot> slots = new List<MonocleModelMaterialSlot>();

            var pointers = parts[mesh];

            foreach (var mat in pointers)
			{
				slots.Add(new MonocleModelMaterialSlot(mat));
			}

			var value = new MonocleModel(slots);

			return value;
		}
		public Dictionary<string, MonocleModel> GetMeshes() {
			Dictionary<string, MonocleModel> retval = new Dictionary<string, MonocleModel>();

			foreach (var key in parts.Keys) {
				retval[key] = GetMesh(key);
			}

			return retval;
		}
		public MonocleArmature GetArmature(string armature) {
			return armatures[armature].CreateCopy();
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
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct MonocleVertexWeight : IVertexType {

		/// <summary>
		/// Weight
		/// </summary>
		public float Weight0;
		public float Weight1;
		public float Weight2;
		public float Weight3;

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
		static MonocleVertexWeight() {
			VertexElement[] elements = new VertexElement[] {
				new VertexElement(0 * 4, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 0),
				new VertexElement(1 * 4, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 1),
				new VertexElement(2 * 4, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 2),
				new VertexElement(3 * 4, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 3),
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
		public MonocleVertexWeight(float weightA, float weightB, float weightC, float weightD) {
			Weight0 = weightA;
			Weight1 = weightB;
			Weight2 = weightC;
			Weight3	= weightD;
		}
	}

	public class MonocleModelPart {

		internal MeshPointer MeshData;
		internal VertexBuffer WeightData;
		internal string[] BoneNames;

		public MonocleModelPart(MeshPointer pointer, VertexBuffer weights, params string[] boneNames) {
			MeshData = pointer;
			WeightData = weights;
			BoneNames = boneNames;
        }
    }

    public sealed class MonocleModelMaterialSlot : ReadOnlyCollection<MonocleModelPart>
    {
        public MonocleModelMaterialSlot(IList<MonocleModelPart> list) : base(list)
        {
        }

        internal void Render(Matrix matrix, Material material, MonocleArmature armature = null)
        {
            var mat = material ?? Draw.DefaultMaterial;

            Draw.CustomDrawCall(new ModelRenderCall()
            {
                armature = armature,
                mesh = this,
                material = mat,
                transform = matrix,
                RenderOrder = mat.RenderOrder ?? Draw.CurrentRenderOrder,
            });
        }
    }

    public class MonocleModel : ReadOnlyCollection<MonocleModelMaterialSlot>
    {

        public MonocleModel(IList<MonocleModelMaterialSlot> list) : base(list)
        {
        }

        public void Render(Matrix matrix, params Material[] Materials)
		{

			for (int i = 0; i < Count; i++)
            {
				var mesh = this[i];
				var mat = Materials[i]??Draw.DefaultMaterial;

                Draw.CustomDrawCall(new ModelRenderCall()
                {
                    mesh = mesh,
                    material = mat,
                    transform = matrix,
                    RenderOrder = mat.RenderOrder ?? Draw.CurrentRenderOrder,
                });
            }
        }
        public void Render(Matrix matrix, MonocleArmature armature, params Material[] Materials)
        {

			for (int i = 0; i < Count; i++)
			{
				this[i].Render(matrix, Materials[i], armature);
			}
		}
    }
}
