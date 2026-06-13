using FMOD;
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
using System.Xml.Linq;
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

		static DynamicVertexBuffer emptyWeightBuffer;

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

			emptyWeightBuffer = new DynamicVertexBuffer(device, typeof(MonocleVertexWeight), 0x1000, BufferUsage.None);

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
			vertices.Add((new DynamicVertexBuffer(graphics, typeof(MonocleVertex), 0x20000, BufferUsage.None), new GenericHeap(0x20000)));
		}
		static void AddWeightBuffer() {
			Engine.WaitForRendering();
			weights.Add((new DynamicVertexBuffer(graphics, typeof(MonocleVertexWeight), 0x20000, BufferUsage.None), new GenericHeap(0x20000)));
		}
		static void AddIndexBuffer() {
			Engine.WaitForRendering();
			indices.Add((new DynamicIndexBuffer(graphics, IndexElementSize.SixteenBits, 0xC0000, BufferUsage.None), new GenericHeap(0xC0000)));
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
        static (VertexBuffer, int) GetWeightBuffer(MonocleVertexWeight[] data)
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

            var buffer = (weights[index].buffer, weights[index].heap.AddRange(data.Length));
            buffer.Item1.SetData(buffer.Item2 * MonocleVertexWeight.VertexDeclaration.VertexStride, data, 0, data.Length, MonocleVertexWeight.VertexDeclaration.VertexStride);

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


				return new MeshPointer(graphics, vb.Item1, ib.Item1, emptyWeightBuffer, vb.Item2, ib.Item2, inds.Length / 3, 0);

			}
			catch {
				return null;
			}
        }
        public static MeshPointer CreateSection(MonocleVertex[] verts, short[] inds, MonocleVertexWeight[] weights)
        {


            try
            {
                var vb = GetVBuffer(verts);
                var ib = GetIBuffer(inds, vb.Item2);
				bool hasWeight = false;

                foreach (var w in weights)
                {
                    if (w.Weight0 > 0 || w.Weight1 > 0 || w.Weight2 > 0 || w.Weight3 > 0)
                    {
                        hasWeight = true;
                        break;
                    }
                }

                if (hasWeight)
                {
                    var wb = GetWeightBuffer(weights);
                    return new MeshPointer(graphics, vb.Item1, ib.Item1, wb.Item1, vb.Item2, ib.Item2, inds.Length / 3, wb.Item2);
                }
                else
                {
                    return new MeshPointer(graphics, vb.Item1, ib.Item1, emptyWeightBuffer, vb.Item2, ib.Item2, inds.Length / 3, 0);
                }

            }
            catch
            {
                return null;
            }
        }
        public static MeshPointer[] CreateSections(MonocleVertex[] verts, short[][] inds) {

			try {
				MeshPointer[] pointers = new MeshPointer[inds.Length];

				var vb = GetVBuffer(verts);

				for (int i = 0; i < inds.Length; i++) {
					var ib = GetIBuffer(inds[i], vb.Item2);

					pointers[i] = new MeshPointer(graphics, vb.Item1, ib.Item1, emptyWeightBuffer, vb.Item2, ib.Item2, inds[i].Length / 3, 0);

				}

				return pointers;
			}
			catch {
				return null;
			}
        }

        public static MeshPointer[] CreateSections(MonocleVertex[] verts, short[][] inds, MonocleVertexWeight[] weights)
        {
			if (weights != null && verts.Length != weights.Length)
				throw new Exception();

			if (verts.Length == 0)
				return null;


            MeshPointer[] pointers = new MeshPointer[inds.Length];

			var vb = GetVBuffer(verts);
			(VertexBuffer, int) wb = default;

			foreach (var w in weights) {
				if (w.Weight0 > 0 || w.Weight1 > 0 || w.Weight2 > 0 || w.Weight3 > 0) {
					wb = GetWeightBuffer(weights);
					break;
				}
			}

            for (int i = 0; i < inds.Length; i++)
            {
                var ib = GetIBuffer(inds[i], vb.Item2);


				if (wb.Item1 != null)
				{
					pointers[i] = new MeshPointer(graphics, vb.Item1, ib.Item1, wb.Item1, vb.Item2, ib.Item2, inds[i].Length / 3, wb.Item2);
				}
				else
				{
					pointers[i] = new MeshPointer(graphics, vb.Item1, ib.Item1, emptyWeightBuffer, vb.Item2, ib.Item2, inds[i].Length / 3, 0);
				}

            }

            return pointers;
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
		public VertexBuffer WeightBuffer { get; internal set; }
		public int VertexOffset { get; internal set; }
		public int WeightOffset { get; internal set; }
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
        public MeshPointer(GraphicsDevice graphicsDevice, VertexBuffer vbuffer, IndexBuffer ibuffer, VertexBuffer wbuffer, int voffset, int ioffset, int count, int woffset)
        {
            gd = graphicsDevice;
            VertexBuffer = vbuffer;
            IndexBuffer = ibuffer;
			WeightBuffer = wbuffer;
            VertexOffset = voffset;
            IndexOffset = ioffset;
			WeightOffset = woffset;
            PrimitiveCount = count;
        }

        public void SetIndex()
        {
            gd.Indices = IndexBuffer;
            if (WeightBuffer != null)
			{
				gd.SetVertexBuffers(new VertexBufferBinding(VertexBuffer, VertexOffset), new VertexBufferBinding(WeightBuffer, WeightOffset));
			}
			else
			{
				gd.SetVertexBuffers(new VertexBufferBinding(VertexBuffer, VertexOffset));
			}
		}
		public void SetIndex(VertexBuffer extra, int offset) {
			if (extra == null)
			{
				SetIndex();
				return;

            }

            if (WeightBuffer != null)
            {
                gd.SetVertexBuffers(new VertexBufferBinding(VertexBuffer, VertexOffset), new VertexBufferBinding(WeightBuffer, WeightOffset), new VertexBufferBinding(extra, offset));
            }
            else
            {
                gd.SetVertexBuffers(new VertexBufferBinding(VertexBuffer, VertexOffset), new VertexBufferBinding(extra, offset));
            }

            
			gd.Indices = IndexBuffer;
        }
        public void RenderList()
        {
            if (PrimitiveCount <= 0)
                return;

            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, IndexOffset, PrimitiveCount);
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

	
	public class MonocleBone {

		List<MonocleBone> children;
		public MonocleBone Parent { get; private set; }
		MonocleArmature armature;

		public string Name { get; private set; }

		public bool CopyScale = true;

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
        public Quaternion GlobalRotation
        {
            get {
				var rot = localLocalRotation;

				if (Parent != null) {
					rot = Parent.GlobalRotation * rot;
				}
				return rot;
			}
            set
            {
                rotation = value;
                localLocalRotation = Quaternion.Inverse(Rotation_orig) * value * Rotation_orig;
            }
        }

        public Matrix Transform {
			get {

				Vector3 pos = Position_orig;


				var mat = Matrix.CreateTranslation(-pos) * LocalTransform;

                mat *= Matrix.CreateTranslation(pos);

                if (Parent != null)
				{
					var newMat = mat * Matrix.CreateScale(Parent.LocalScale);

					mat = newMat;
					mat *= Matrix.CreateFromQuaternion(Parent.GlobalRotation);
				}


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
		private Vector3 TransformPoint(Vector3 pos) {

			if (Parent != null) {
				pos = Parent.TransformPoint(pos);
			}

			pos = Vector3.Transform(pos, Transform);

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
		static void RecalculateNormals(VertexData[] vertices) {

			var indices = new int[vertices.Length];
			for (int i = 0; i < vertices.Length; i++) {
				indices[i] = (short)indices[i];
			}
			RecalculateNormals(vertices, indices);
		}
		static void CalculateTangent(VertexData[] vertices) {

			var indices = new int[vertices.Length];
			for (int i = 0; i < vertices.Length; i++) {
				indices[i] = (short)indices[i];
			}
			CalculateTangent(vertices, indices);
		}
		static void RecalculateNormals(VertexData[] vertices, int[] indices) {

			Vector3[] newNormals = new Vector3[vertices.Length];
			Vector3[] newBi = new Vector3[vertices.Length];
			Vector3[] newTan = new Vector3[vertices.Length];

			for (int i = 0; i < indices.Length; i += 3) {
				Vector3 a = vertices[indices[i]].vertex.Position;
				Vector3 b = vertices[indices[i + 1]].vertex.Position;
				Vector3 c = vertices[indices[i + 2]].vertex.Position;

				Vector3 n = -Vector3.Cross(b - a, c - a).SafeNormalize();

				Vector2 h = vertices[indices[i]].vertex.TextureCoordinate;
				Vector2 k = vertices[indices[i + 1]].vertex.TextureCoordinate;
				Vector2 l = vertices[indices[i + 2]].vertex.TextureCoordinate;

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

				vertices[i].vertex.Normal = newNormals[i].SafeNormalize();
				vertices[i].vertex.Binormal = newBi[i].SafeNormalize();
				vertices[i].vertex.Tangent = newTan[i].SafeNormalize();
			}
		}
		static void CalculateTangent(VertexData[] vertices, int[] indices) {

			Vector3[] newBi = new Vector3[vertices.Length];
			Vector3[] newTan = new Vector3[vertices.Length];

			for (int i = 0; i < indices.Length; i += 3) {
				Vector3 a = vertices[indices[i]].vertex.Position;
				Vector3 b = vertices[indices[i + 1]].vertex.Position;
				Vector3 c = vertices[indices[i + 2]].vertex.Position;

				Vector2 h = vertices[indices[i]].vertex.TextureCoordinate;
				Vector2 k = vertices[indices[i + 1]].vertex.TextureCoordinate;
				Vector2 l = vertices[indices[i + 2]].vertex.TextureCoordinate;

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
				vertices[i].vertex.Binormal = newBi[i].SafeNormalize();
				vertices[i].vertex.Tangent = newTan[i].SafeNormalize();
			}
		}
		static void RecalculateNormals(VertexData[] vertices, int[][] indices) {
			List<int> newInds = new List<int>();

			foreach (var item in indices) {
				foreach (var i in item) {
					newInds.Add(i);
				}
			}

			RecalculateNormals(vertices, newInds.ToArray());

		}
		static void CalculateTangent(VertexData[] vertices, int[][] indices) {
			List<int> newInds = new List<int>();

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
			public string BoneA, BoneB, BoneC, BoneD;
			public float WeightA, WeightB, WeightC, WeightD;
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

                val.A = data.BoneA;
                val.B = data.BoneB;
                val.C = data.BoneC;
                val.D = data.BoneD;

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

            VertexData[] vertices = new VertexData[] {
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3(-1, -1, -1), Color = Vector4.One }, VertexIndex = 0},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3( 1, -1, -1), Color = Vector4.One }, VertexIndex = 1},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3(-1,  1, -1), Color = Vector4.One }, VertexIndex = 2},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3( 1,  1, -1), Color = Vector4.One }, VertexIndex = 3},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3(-1, -1,  1), Color = Vector4.One }, VertexIndex = 4},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3( 1, -1,  1), Color = Vector4.One }, VertexIndex = 5},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3(-1,  1,  1), Color = Vector4.One }, VertexIndex = 6},
				new VertexData(){ vertex = new MonocleVertex() {Position = new Vector3( 1,  1,  1), Color = Vector4.One }, VertexIndex = 7},
			};
			int[][] indices = new int[][]
			{
				new int[]
				{
					0, 3, 1,
					0, 2, 3,

					4, 5, 6,
					5, 7, 6,

					1, 5, 4,
					0, 1, 4,

					2, 6, 3,
					7, 3, 6
				}
			};
			Dictionary<string, float[]> weights = new Dictionary<string, float[]>()
			{
				{"Top",		new float[]{ 0, 0, 1, 1, 0, 0, 1, 1 } },
				{"Bottom",	new float[]{ 1, 1, 0, 0, 1, 1, 0, 0 } },
			};

			retval.modelSegments.Add("Tester", CreateModel(vertices, indices, weights));

			return retval;
        }

		public static FBXScene Import(LoadedAsset content) {

			FBXScene retval = new FBXScene();

			var array = FBXNode.GetFBX(content);

			retval.ImportFromNode(array);

			return retval;
		}

		static MonocleModelPart[][] CreateModel(VertexData[] verts, int[][] pvi, Dictionary<string, float[]> weightDict)
        {
            CalculateTangent(verts, pvi);


            List<(int, BonePairs)> totalIndices = new List<(int, BonePairs)>();
            List<MonocleModelPart[]> modelWhole = new List<MonocleModelPart[]>();
            List<MonocleModelPart> parts = new List<MonocleModelPart>();

            int totalI = 0;

            foreach (var inds in pvi)
            {
                Dictionary<BonePairs, List<int>> segments = new Dictionary<BonePairs, List<int>>();

                for (int i = 0; i < inds.Length; i += 3)
                {
                    Dictionary<string, float> values = new Dictionary<string, float>();

                    foreach (var weight in weightDict)
                    {
                        values[weight.Key] = weight.Value[verts[inds[i]].VertexIndex];
                        values[weight.Key] += weight.Value[verts[inds[i + 1]].VertexIndex];
                        values[weight.Key] += weight.Value[verts[inds[i + 2]].VertexIndex];
                    }

                    var pairs = BonePairs.Get(values);
                    if (!segments.ContainsKey(pairs))
                    {
                        segments[pairs] = new List<int>();
                    }
                    segments[pairs].Add(i);

                    verts[inds[i]].BoneA = pairs.A;
                    verts[inds[i]].BoneB = pairs.B;
                    verts[inds[i]].BoneC = pairs.C;
                    verts[inds[i]].BoneD = pairs.D;

                    verts[inds[i + 1]].BoneA = pairs.A;
                    verts[inds[i + 1]].BoneB = pairs.B;
                    verts[inds[i + 1]].BoneC = pairs.C;
                    verts[inds[i + 1]].BoneD = pairs.D;

                    verts[inds[i + 2]].BoneA = pairs.A;
                    verts[inds[i + 2]].BoneB = pairs.B;
                    verts[inds[i + 2]].BoneC = pairs.C;
                    verts[inds[i + 2]].BoneD = pairs.D;
                }

                foreach (var item in segments)
                {
                    totalIndices.Add((totalI, item.Key));
                }

                totalI++;
            }

            List<short[]> totalCompiled = new List<short[]>();
			Dictionary<VertexData, int> data = new Dictionary<VertexData, int>();
			List<MonocleVertexWeight> weightTotal = new List<MonocleVertexWeight>();

            for (int i = 0; i < totalIndices.Count; i++)
            {
                var p = totalIndices[i];


                short[] inds = new short[pvi[p.Item1].Length];

                for (int j = 0; j < inds.Length; j++)
                {
					var t = pvi[p.Item1][j];
                    var vert = verts[t];

					if (!data.ContainsKey(vert))
					{
						data[vert] = data.Count;

						weightTotal.Add(new MonocleVertexWeight()
						{
							Weight0 = vert.BoneA == null ? 0 : weightDict[vert.BoneA][vert.VertexIndex],
							Weight1 = vert.BoneB == null ? 0 : weightDict[vert.BoneB][vert.VertexIndex],
							Weight2 = vert.BoneC == null ? 0 : weightDict[vert.BoneC][vert.VertexIndex],
							Weight3 = vert.BoneD == null ? 0 : weightDict[vert.BoneD][vert.VertexIndex],
						});
                    }

                    //vertices[j] = vert.vertex;
                    inds[j] = (short)data[vert];

                }

				totalCompiled.Add(inds);

            }

			totalI = 0;
			var pointers = MeshHeap.CreateSections(data.Keys.Select(a => a.vertex).ToArray(), totalCompiled.ToArray(), weightTotal.ToArray());
			for (int i = 0; i < totalIndices.Count; i++)
			{
                var p = totalIndices[i];

				if (p.Item1 != totalI) {
					totalI = p.Item1;
					modelWhole.Add(parts.ToArray());
					parts.Clear();
				}

				parts.Add(new MonocleModelPart(pointers[i], p.Item2.A, p.Item2.B, p.Item2.C, p.Item2.D));
            }

            modelWhole.Add(parts.ToArray());

            return modelWhole.ToArray();
		}



        Dictionary<string, MonocleModelPart[][]> modelSegments = new Dictionary<string, MonocleModelPart[][]>();
		Dictionary<string, MonocleArmature> armatures = new Dictionary<string, MonocleArmature>();
        Dictionary<long, List<long>> parent2Child = new Dictionary<long, List<long>>();
        Dictionary<long, List<long>> child2Parent = new Dictionary<long, List<long>>();
        Dictionary<long, FBXNode> nodeIDs = new Dictionary<long, FBXNode>();

        FBXNode FileNode;

		FBXScene() {
		}

		MonocleBone GetBone(FBXNode child, Matrix parentMatrix)
		{
            long uuid = (long)child.properties[0];
            string name = ((string)child.properties[1]).Split("\0\u0001")[0];
            var type = ((string)child.properties[2]);

			var properties = child["Properties70"];

			var matrix = Matrix.Identity;
			var rotationMatrix = Matrix.Identity;

			foreach (var pr in properties.children)
			{
				switch (pr.properties[0].ToString())
				{
					case "Lcl Translation":
						var test = Convert.ToSingle(pr.properties[5].ToString());
                        Vector3 pos = new Vector3(
                            Convert.ToSingle(pr.properties[4]),
                            Convert.ToSingle(pr.properties[5]),
                            Convert.ToSingle(pr.properties[6])
                            );
                        matrix *= Matrix.CreateTranslation(pos);
						break;
					case "Lcl Rotation":
						Vector3 rot = new Vector3(
                            MathHelper.ToRadians(Convert.ToSingle(pr.properties[4].ToString())),
                            MathHelper.ToRadians(Convert.ToSingle(pr.properties[5].ToString())),
                            MathHelper.ToRadians(Convert.ToSingle(pr.properties[6].ToString()))
                            );
                        var mat = Matrix.CreateFromQuaternion(Calc.EulerAngle(rot));
						rotationMatrix *= mat;
						matrix *= mat;
						break;
					case "Lcl Scaling":
                        Vector3 sc = new Vector3(
                            Convert.ToSingle(pr.properties[4]),
                            Convert.ToSingle(pr.properties[5]),
                            Convert.ToSingle(pr.properties[6])
                            );
                        matrix *= Matrix.CreateScale(sc);
						break;
				}
			}
			matrix = parentMatrix * matrix;

            var bone = new MonocleBone(name, matrix);

			if (parent2Child.ContainsKey(uuid))
			{
				foreach (var val in parent2Child[uuid])
				{
					var node = nodeIDs[val];
					if (node.name == "Model")
					{
						var childBone = GetBone(node, matrix);
						if (childBone != null)
						{
							bone.AddChild(childBone);
						}
					}
				}
			}

			return bone;

		}

        void ImportFromNode(FBXNode array)
        {
			FileNode = array;

            List<FBXNode> meshes = new List<FBXNode>();
            List<FBXNode> arms = new List<FBXNode>();

            foreach (var child in array["Connections"].children)
            {
                long parentID = (long)child.properties[2];
                long childID = (long)child.properties[1];

                if (parentID != 0)
                {
                    if (!parent2Child.ContainsKey(parentID))
                        parent2Child[parentID] = new List<long>();
                    parent2Child[parentID].Add(childID);

                    if (!child2Parent.ContainsKey(childID))
                        child2Parent[childID] = new List<long>();
                    child2Parent[childID].Add(parentID);
                }
            }
            foreach (var child in array["Objects"].children)
            {
                nodeIDs.Add((long)child.properties[0], child);
                if (child.name == "Model")
                {
                    if ((string)child.properties[2] == "Mesh")
                    {
                        meshes.Add(child);
                    }
                    else if ((string)child.properties[2] == "Null")
                    {
                        arms.Add(child);
                    }
                }
            }

            foreach (var mesh in meshes)
            {
                long uuid = (long)mesh.properties[0];
                string[] split = ((string)mesh.properties[1]).Split("\0\u0001");
                var type = ((string)mesh.properties[2]);

                if (!parent2Child.ContainsKey(uuid))
                    continue;

                VertexData[] verts = null;
				int[][] indices = null;
                Dictionary<string, float[]> weights = null;

                foreach (var val in parent2Child[uuid])
                {
                    var child = nodeIDs[val];
					var localUUID = (long)child.properties[0];
                    if (child.name == "Geometry")
                    {
                        FBXNode deformer = null;

                        if (parent2Child.ContainsKey(localUUID))
                        {
                            foreach (var val2 in parent2Child[localUUID])
                            {
								var n = nodeIDs[val2];
                                if (nodeIDs[val2].name == "Deformer" && nodeIDs[val2].properties[2] as string == "Skin")
                                {
                                    deformer = nodeIDs[val2];
                                }
                            }
                        }

                        var pvi = child["PolygonVertexIndex"].properties[0] as int[];
                        var dArray = child["Vertices"].properties[0] as double[];

                        verts = new VertexData[pvi.Length];

                        weights = new Dictionary<string, float[]>();

                        if (deformer != null)
                        {
                            foreach (var value in getWeights(dArray.Length / 3, deformer))
                            {
                                weights.Add(value.Item1, value.Item2);
                            }
                        }

                        // Triangulating faces
                        List<int> faceCount = new List<int>();
                        int c = 0;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            c++;
                            int index = pvi[i];
                            if (index < 0)
                            {
                                index = ~index;
                                faceCount.Add(c);
                                c = 0;
                            }
                            verts[i] = new VertexData() { VertexIndex = index };
                            verts[i].vertex.Position = new Vector3((float)dArray[index * 3], (float)dArray[index * 3 + 2], -(float)dArray[index * 3 + 1]);
                        }

                        // Other properties
                        int idx = 0;
                        if (child[$"LayerElementColor"] == null)
                        {
                            for (int i = 0; i < verts.Length; i++)
                            {
                                verts[i].vertex.Color = Vector4.One;
                            }
                        }
                        else
                        {
                            foreach (Vector4 item in GetArray<Vector4>(child, pvi, "Color"))
                            {
                                verts[idx++].vertex.Color = item;
                            }
                        }
                        idx = 0;
                        foreach (Vector2 item in GetArray<Vector2>(child, pvi, "UV"))
                        {
                            verts[idx++].vertex.TextureCoordinate = new Vector2(item.X, 1 - item.Y);
                        }
                        idx = 0;
                        foreach (Vector3 item in GetArray<Vector3>(child, pvi, "Normal"))
                        {
                            verts[idx++].vertex.Normal = new Vector3(item.X, item.Z, -item.Y);
                        }

                        List<List<int>> finalIndices = new List<List<int>>();


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

                                    List<int> indices2 = new List<int>();
                                    idx = 0;
                                    for (int i = 0; i < faceCount.Count; i++)
                                    {
                                        for (int j = 0; j < faceCount[i] - 2; j++)
                                        {
                                            indices2.Add(idx);
                                            indices2.Add(idx + j + 1);
                                            indices2.Add(idx + j + 2);
                                        }
                                        idx += faceCount[i];
                                    }
                                    finalIndices.Add(indices2);
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
                                            finalIndices[index].Add(idx);
                                            finalIndices[index].Add(idx + j + 1);
                                            finalIndices[index].Add(idx + j + 2);
                                        }
                                        idx += faceCount[i];
                                    }
                                    break;
                                }
                        }

						indices = finalIndices.Select((l) => { return l.ToArray(); }).ToArray();

                        break;
                    }
                }


                modelSegments[split[0]] = CreateModel(verts, indices, weights);
            }

            foreach (var arm in arms)
            {
                long uuid = (long)arm.properties[0];
                string[] split = ((string)arm.properties[1]).Split("\0\u0001");
                var type = ((string)arm.properties[2]);

                var armature = armatures[split[0]] = new MonocleArmature();

                if (parent2Child.ContainsKey(uuid))
                {
                    foreach (var val in parent2Child[uuid])
                    {
                        var node = nodeIDs[val];
                        if (node.name == "Model")
                        {
                            var bone = GetBone(node, Matrix.CreateFromQuaternion(Calc.EulerAngle(MathHelper.PiOver2, 0, 0)));
                            armature.AddBone(bone);
                        }
                    }
                }
            }

        }
        IEnumerable<(string, float[])> getWeights(int vertLength, FBXNode parentNode)
        {
			if (!parent2Child.ContainsKey((long)parentNode.properties[0]))
				yield break;

            foreach (var val in parent2Child[(long)parentNode.properties[0]])
            {
                var node = nodeIDs[val];

                if (node.name == "Model" || node.properties[2] as string == "LimbNode")
                {
                    foreach (var weight in getWeights(vertLength, node))
                    {
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
                foreach (var weight in getWeights(vertLength, node))
                {
                    yield return weight;
                }

            }
        }
        IEnumerable GetArray<T>(FBXNode node, int[] pvi, string name)
        {

            if (node[name] == null)
            {
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

            if (typeof(T) == typeof(Vector2) || typeof(T) == typeof(Vector3) || typeof(T) == typeof(Vector4))
            {
                float[] fArray;
                if (data.properties[0] is double[])
                {
                    fArray = ((double[])data.properties[0]).Select(x => (float)x).ToArray();
                }
                else if (data.properties[0] is long[])
                {
                    fArray = ((long[])data.properties[0]).Select(x => (float)x).ToArray();
                }
                else if (data.properties[0] is int[])
                {
                    fArray = ((int[])data.properties[0]).Select(x => (float)x).ToArray();
                }
                else
                {
                    fArray = (float[])data.properties[0];
                }

                int[] iArray = (int[])indices.properties[0];

                if (node["MappingInformationType"].properties[0].ToString() == "ByVertice")
                {
                    int[] newArray = new int[pvi.Length];

                    for (int i = 0; i < pvi.Length; i++)
                    {
                        int v = pvi[i];
                        if (v < 0)
                            v = ~v;
                        newArray[i] = iArray[v];
                    }

                    iArray = newArray;
                }

                if (typeof(T) == typeof(Vector2))
                {
                    for (int i = 0; i < iArray.Length; i++)
                    {
                        int idx = iArray[i] * 2;
                        yield return new Vector2(fArray[idx], fArray[idx + 1]);
                    }
                }
                else if (typeof(T) == typeof(Vector3))
                {
                    for (int i = 0; i < iArray.Length; i++)
                    {
                        int idx = iArray[i] * 3;
                        yield return new Vector3(fArray[idx], fArray[idx + 1], fArray[idx + 2]);
                    }
                }
                else if (typeof(T) == typeof(Vector4))
                {
                    for (int i = 0; i < iArray.Length; i++)
                    {
                        int idx = iArray[i] * 4;
                        yield return new Vector4(fArray[idx], fArray[idx + 1], fArray[idx + 2], fArray[idx + 3]);
                    }
                }
            }


            yield break;
        }

        public MonocleModel CreateMesh(string mesh) {

			List<MonocleModelMaterialSlot> slots = new List<MonocleModelMaterialSlot>();

            var pointers = modelSegments[mesh];

            foreach (var mat in pointers)
			{
				slots.Add(new MonocleModelMaterialSlot(mat));
			}

			var value = new MonocleModel(slots);

			return value;
		}
		public Dictionary<string, MonocleModel> CreateMeshes() {
			Dictionary<string, MonocleModel> retval = new Dictionary<string, MonocleModel>();

			foreach (var key in modelSegments.Keys) {
				retval[key] = CreateMesh(key);
			}

			return retval;
		}
		public MonocleArmature CreateArmature(string armature) {
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
		internal string[] BoneNames;

		public MonocleModelPart(MeshPointer pointer, params string[] boneNames) {
			MeshData = pointer;
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
			ModelRenderCall.AddModelCalls(this, matrix, material, armature);
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

				ModelRenderCall.AddModelCalls(mesh, matrix, mat);
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
