using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Monocle {
	public class BinaryFileNode {
		public string Name;
		public Dictionary<string, object> Attributes = new Dictionary<string, object>();
		public List<BinaryFileNode> Children = new List<BinaryFileNode>();
		private List<string> compressedStrings = new List<string>();

		private object Dump;

		public T Decompile<T>(bool fromChild) {
			return (T)Decompile(typeof(T), fromChild);
		}
		public object Decompile(Type rootType, bool fromChild) {
			if (rootType == typeof(object)) {
				rootType = Type.GetType(GetString($"cst_{Name}"));
			}
			switch (rootType.FullName) {
				case "System.Boolean":
				case "System.Int64":
				case "System.Int32":
				case "System.Int16":
				case "System.Byte":
				case "System.String":
				case "System.Single":
				case "System.Double":
				case "System.Byte[]":
				case "Microsoft.Xna.Framework.Vector2":
				case "Microsoft.Xna.Framework.Vector3":
				case "Microsoft.Xna.Framework.Vector4":
					return Attributes["value"];
				default:
					if (rootType.IsArray) {
						DecompileField(this, typeof(BinaryFileNode).GetField("Dump", BindingFlags.NonPublic | BindingFlags.Instance), null, true);

						return Dump;
					}
					break;
			}

			object retval = Activator.CreateInstance(rootType);


			void DecompileField(BinaryFileNode rootNode, FieldInfo info, object obj, bool toChild) {

				switch (info.FieldType.FullName) {
					case "System.Boolean":
					case "System.Int64":
					case "System.Int32":
					case "System.Int16":
					case "System.Byte":
					case "System.String":
					case "System.Single":
					case "System.Double":
					case "System.Byte[]":
					case "Microsoft.Xna.Framework.Vector2":
					case "Microsoft.Xna.Framework.Vector3":
					case "Microsoft.Xna.Framework.Vector4":
						if (toChild) {
							if (rootNode[info.Name] != null)
								info.SetValue(obj, rootNode[info.Name].Attributes["value"]);
						}
						else {
							if (rootNode.Attributes.ContainsKey(info.Name))
								info.SetValue(obj, rootNode.Attributes[info.Name]);
						}
						break;
					default:
						if (rootNode.GetChild(info.Name) == null)
							break;

						if (info.FieldType.IsArray) {
							var arrayType = info.FieldType.GetElementType();

							BinaryFileNode child = rootNode.GetChild(info.Name);

							int length = child.GetInteger("length");

							dynamic array = Activator.CreateInstance(arrayType.MakeArrayType(), length);

							switch (arrayType.FullName) {
								case "System.Boolean":
								case "System.Int64":
								case "System.Int32":
								case "System.Int16":
								case "System.Byte":
								case "System.String":
								case "System.Single":
								case "System.Double":
								case "System.Byte[]":
								case "Microsoft.Xna.Framework.Vector2":
								case "Microsoft.Xna.Framework.Vector3":
								case "Microsoft.Xna.Framework.Vector4":
									for (int i = 0; i < length; ++i) {
										if (child.Attributes.ContainsKey($"child{i}")) {
											array.SetValue(child.Attributes[$"child{i}"], i);
										}
									}
									break;
								default:
									for (int i = 0; i < length; ++i) {
										var subChild = child.GetChild($"child{i}");
										if (subChild != null) {
											array.SetValue(subChild.Decompile(arrayType, false), i);
										}
										else if (child.Attributes.ContainsKey($"child{i}")) {
											array.SetValue(child.Attributes[$"child{i}"]);
										}
									}
									break;
							}

							info.SetValue(obj, array);
						}
						else {
							// List and dictionary
							if (info.FieldType.IsGenericType) {
								var typeDef = info.FieldType.GetGenericTypeDefinition();

								if (info.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {

									var keyType = info.FieldType.GetGenericArguments()[0];
									var valueType = info.FieldType.GetGenericArguments()[1];


									if (keyType == typeof(int)) {
										
										dynamic dict = Activator.CreateInstance(info.FieldType);

										foreach (var item in rootNode["values"].Children) {

											dict[int.Parse(item.Name)] = rootNode.Decompile(valueType, true);
										}

										info.SetValue(obj, dict);
									}
									else if (keyType == typeof(string)) {

										dynamic dict = Activator.CreateInstance(info.FieldType);

										foreach (var item in rootNode["values"].Children) {

											dict[item.Name] = item.Decompile(valueType, true);
										}

										info.SetValue(obj, dict);
									}
									else if (keyType == typeof(char)) {

										dynamic dict = Activator.CreateInstance(info.FieldType);

										foreach (var item in rootNode["values"].Children) {

											dict[item.Name[0]] = rootNode.Decompile(valueType, true);
										}

										info.SetValue(obj, dict);
									}
								}
								else if (info.FieldType.GetGenericTypeDefinition() == typeof(List<>)) {

									throw new NotImplementedException();
									//var valueType = info.FieldType.GetGenericArguments()[0];

									//BinaryFileNode child = rootNode.AddChild(info.Name);

									//dynamic list = obj;

									//foreach (var item in rootNode.Children) {

									//}
									//for (var i = 0; i < list.Count; i++) {
									//	//CompileField(child, $"child{i}", valueType, list[i]);
									//}
								}
							}
							else {
								BinaryFileNode child = rootNode.AddChild(info.Name);
							}
						}
						break;
				}
			}

			foreach (var field in rootType.GetFields(BindingFlags.Public | BindingFlags.Instance)) {

				DecompileField(this, field, retval, fromChild);
			}


			return retval;
		}

		private uint boolBinary, boolIndex;

		public BinaryFileNode this[string name] {
			get {
				foreach (var node in Children) {
					if (node.Name == name)
						return node;
				}
				return null;
			}
		}

		public BinaryFileNode() {
			Name = "EmptyName!";
		}

		#region GetValues
		public BinaryFileNode GetChild(string name) {
			foreach (var child in Children)
				if (child.Name == name)
					return child;

			return null;
		}
		public BinaryFileNode AddChild(string name) {
			var retval = new BinaryFileNode() { Name = name };
			Children.Add(retval);
			return retval;
		}

		public string GetString(string name) {
			var obj = Attributes[name];

			return obj.ToString();
		}
		public int GetInteger(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case int i:
					return i;
				case short i:
					return i;
				case byte i:
					return i;
				case long i:
					return (int)i;
				case uint i:
					return (int)i;
				case ushort i:
					return i;
				case sbyte i:
					return i;
				case ulong i:
					return (int)i;
				case float i:
					return (int)i;
				case double i:
					return (int)i;
			}

			throw new Exception("???");
		}
		public float GetFloat(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case int i:
					return i;
				case short i:
					return i;
				case byte i:
					return i;
				case long i:
					return i;
				case uint i:
					return i;
				case ushort i:
					return i;
				case sbyte i:
					return i;
				case ulong i:
					return i;
				case float i:
					return i;
				case double i:
					return (float)i;
			}

			throw new Exception("???");
		}
		public long GetLong(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case int i:
					return i;
				case short i:
					return i;
				case byte i:
					return i;
				case long i:
					return i;
				case uint i:
					return i;
				case ushort i:
					return i;
				case sbyte i:
					return i;
				case ulong i:
					return (long)i;
				case float i:
					return (long)i;
				case double i:
					return (long)i;
			}

			throw new Exception();
		}
		public double GetDouble(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case int i:
					return i;
				case short i:
					return i;
				case byte i:
					return i;
				case long i:
					return i;
				case uint i:
					return i;
				case ushort i:
					return i;
				case sbyte i:
					return i;
				case ulong i:
					return i;
				case float i:
					return i;
				case double i:
					return i;
			}

			throw new Exception();
		}
		public bool[ ] GetBooleans() {

			List<bool> retval = new List<bool>();

			for (int i = 0; Attributes.ContainsKey($"booleans_{i}"); ++i) {
				uint value = (uint)Attributes[$"booleans_{i}"];
				for (int j = 0; j < 32; ++j) {
					retval.Add((value & 0x1) == 1);
					value >>= 1;
				}
			}

			return retval.ToArray();
		}
		public byte[ ] GetBytes(string name) {
			return Attributes[name] as byte[ ];
		}
		public Vector2 GetVector2(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case Vector2 vector:
					return vector;
				case Vector3 vector:
					return new Vector2(vector.X, vector.Y);
				case Vector4 vector:
					return new Vector2(vector.X, vector.Y);
			}
			throw new FormatException();
		}
		public Vector3 GetVector3(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case Vector2 vector:
					return new Vector3(vector.X, vector.Y, 0);
				case Vector3 vector:
					return vector;
				case Vector4 vector:
					return new Vector3(vector.X, vector.Y, vector.Z);
			}
			throw new FormatException();
		}
		public Vector4 GetVector4(string name) {
			var obj = Attributes[name];

			switch (obj) {
				case Vector2 vector:
					return new Vector4(vector.X, vector.Y, 0, 0);
				case Vector3 vector:
					return new Vector4(vector.X, vector.Y, vector.Z, 0);
				case Vector4 vector:
					return vector;
			}
			throw new FormatException();
		}
		public Matrix GetMatrix(string name) {
			var obj = Attributes[name];

			if (obj is Matrix)
				return (Matrix)obj;

			throw new FormatException();
		}
		#endregion

		// Write Methods
		public void AddBoolean(bool value) {
			int offset = (int)boolIndex & 0x1F;

			boolBinary |= (uint)((value ? 1 : 0) << offset);
			Attributes[$"booleans_{boolIndex >> 5}"] = boolBinary;

			++boolIndex;
			if ((boolIndex & 0x1F) == 0) {
				boolBinary = 0;
			}
		}
		public void AddObject(string name, object value) {
			if (value is char[]) {

			}
			Attributes[name] = value;
			if (compressedStrings.Contains(name))
				compressedStrings.Remove(name);
		}
		public void AddCompressedString(string name, string value) {
			Attributes[name] = value;
			if (!compressedStrings.Contains(name))
				compressedStrings.Add(name);
		}
		
		public override string ToString() {
			return Name;
		}
	}

	public class BinaryFileWriter : BinaryWriter {

		public const byte
			BOOLEAN = 0,
			BYTE = 1,
			SHORT = 2,
			INT = 3,
			FLOAT = 4,
			LOOKUP_STRING = 5,
			DIRECT_STRING = 6,
			COMPRESSED_STRING = 7,
			LONG = 8,
			DOUBLE = 9,
			ENTITY = 10,
			VECTOR2 = 11,
			VECTOR3 = 12,
			VECTOR4 = 13,
			QUATERNION = 14,
			MATRIX = 15,
			BYTE_ARRAY = 16,
			BYTE_ARRAY_COMPRESSED = 17,
			VAL_NULL = 255;

		public BinaryFileNode RootNode;

		public BinaryFileWriter() : base(new MemoryStream()) {
			RootNode = new BinaryFileNode();

			tempValues = BaseStream as MemoryStream;
			bodyWriter = new BinaryWriter(tempValues);
			stringLookup = new List<string>();
		}

		public enum StringType {
			LookUp,
			RunLength,
			Direct,
		}

		readonly BinaryWriter bodyWriter;
		readonly MemoryStream tempValues;

		readonly List<string> stringLookup;

		public override void Write(byte[ ] buffer) {
			bodyWriter.Write(buffer);
		}
		public override void Write(byte[ ] buffer, int index, int count) {
			bodyWriter.Write(buffer, index, count);
		}
		public override void Write(float value) {
			bodyWriter.Write(value);
		}
		public override void Write(bool value) {
			bodyWriter.Write(value);
		}
		public override void Write(byte value) {
			bodyWriter.Write(value);
		}
		public override void Write(char ch) {
			bodyWriter.Write(ch);
		}
		public override void Write(char[ ] chars) {
			bodyWriter.Write(chars);
		}
		public override void Write(char[ ] chars, int index, int count) {
			bodyWriter.Write(chars, index, count);
		}
		public override void Write(decimal value) {
			bodyWriter.Write(value);
		}
		public override void Write(double value) {
			bodyWriter.Write(value);
		}
		public override void Write(int value) {
			bodyWriter.Write(value);
		}
		public override void Write(long value) {
			bodyWriter.Write(value);
		}
		public override void Write(sbyte value) {
			bodyWriter.Write(value);
		}
		public override void Write(short value) {
			bodyWriter.Write(value);
		}
		public override void Write(uint value) {
			bodyWriter.Write(value);
		}
		public override void Write(ulong value) {
			bodyWriter.Write(value);
		}
		public override void Write(ushort value) {
			bodyWriter.Write(value);
		}

		public void WriteGeneric(object obj) {
			switch (obj) {
				default:
					if (obj == null) {
						Write(VAL_NULL);
					}
					break;
				case bool value:
					Write(BOOLEAN);
					Write(value);

					break;
				case byte value:
					Write(BYTE);
					Write(value);

					break;
				case sbyte value:
					Write(BYTE);
					Write(value);

					break;
				case short value:
					Write(SHORT);
					Write(value);

					break;
				case ushort value:
					Write(SHORT);
					Write(value);

					break;
				case int value:
					Write(INT);
					Write(value);

					break;
				case uint value:
					Write(INT);
					Write(value);

					break;
				case float value:
					Write(FLOAT);
					Write(value);

					break;
				case string value:
					Write(LOOKUP_STRING);
					Write(value);

					break;
				case long value:
					Write(LONG);
					Write(value);

					break;
				case ulong value:
					Write(LONG);
					Write(value);

					break;
				case double value:
					Write(DOUBLE);
					Write(value);

					break;
				case byte[ ] value:
					if (value.Length > 64) {

						Write(BYTE_ARRAY_COMPRESSED);
						WriteCompressedBytes(value);
					}
					else {
						Write(BYTE_ARRAY);
						Write((uint)value.Length);
						foreach (var b in value)
							Write(b);
					}

					break;
				case Vector2 v:
					Write(VECTOR2);
					Write(v.X);
					Write(v.Y);
					break;
				case Vector3 v:
					Write(VECTOR3);
					Write(v.X);
					Write(v.Y);
					Write(v.Z);
					break;
				case Vector4 v:
					Write(VECTOR4);
					Write(v.X);
					Write(v.Y);
					Write(v.Z);
					Write(v.W);
					break;
				case Quaternion q:
					Write(QUATERNION);
					Write(q.X);
					Write(q.Y);
					Write(q.Z);
					Write(q.W);
					break;
				case Matrix m:
					Write(MATRIX);
					Write(m.M11);
					Write(m.M12);
					Write(m.M13);
					Write(m.M14);
					Write(m.M21);
					Write(m.M22);
					Write(m.M23);
					Write(m.M24);
					Write(m.M31);
					Write(m.M32);
					Write(m.M33);
					Write(m.M34);
					Write(m.M41);
					Write(m.M42);
					Write(m.M43);
					Write(m.M44);
					break;
			}
		}
		public void WriteString(string value, StringType type) {
			switch (type) {
				case StringType.Direct:
					Write(DIRECT_STRING);
					bodyWriter.Write(value);
					break;
				case StringType.LookUp:
					Write(LOOKUP_STRING);
					Write(value);
					break;
				case StringType.RunLength:
					Write(COMPRESSED_STRING);


					char prev = value[0];
					byte len = 0;

					List<byte> bytes = new List<byte>();

					for (int i = 0; i < value.Length; ++i) {
						++len;
						if (prev != value[i] || len == 255) {
							bytes.Add(len);
							bytes.Add((byte)prev);

							prev = value[i];
							len = 0;
						}
					}
					if (len != 0) {
						bytes.Add(len);
						bytes.Add((byte)prev);
					}

					Write((short)bytes.Count);
					Write(bytes.ToArray());

					break;
			}
		}
		public void WriteCompressedBytes(byte[] value) {

			byte prev = value[0];
			byte len = 0;

			List<byte> bytes = new List<byte>();

			for (int i = 0; i < value.Length; ++i) {
				if (prev != value[i] || len == 255) {
					bytes.Add(len);
					bytes.Add(prev);

					prev = value[i];
					len = 0;
				}
				++len;
			}
			if (len != 0) {
				bytes.Add(len);
				bytes.Add(prev);
			}

			Write((short)bytes.Count);
			Write(bytes.ToArray());
		}

		public override void Write(string value) {
			if (value == null)
				throw new Exception();

			if (!stringLookup.Contains(value))
				stringLookup.Add(value);

			bodyWriter.Write((ushort)stringLookup.IndexOf(value));
		}

		private long attrSeek = -1;
		private byte attrCount = 0;
		private void BeginAttributes() {
			if (attrSeek >= 0)
				throw new Exception();

			attrCount = 0;
			attrSeek = tempValues.Position;

			bodyWriter.Write((byte)0);
		}
		private void EndAttributes() {
			if (attrSeek == -1)
				throw new Exception();

			tempValues.Seek(attrSeek, SeekOrigin.Begin);

			bodyWriter.Write(attrCount);

			tempValues.Seek(0, SeekOrigin.End);

			attrSeek = -1;
		}
		public void WriteAttribute(string name, object value) {
			if (attrSeek == -1)
				throw new Exception();

			if (value is long && ((long)value) <= int.MaxValue && ((long)value) >= int.MinValue) {
				value = (int)(long)value;
			}
			if (value is double && ((double)value) <= float.MaxValue && ((double)value) >= float.MinValue) {
				value = (float)(double)value;
			}

			Write(name);
			WriteGeneric(value);

			++attrCount;
		}
		public void WriteAttribute(string name, string value, StringType type) {
			if (attrSeek == -1)
				throw new Exception();

			Write(name);
			WriteString(value, type);

			++attrCount;
		}
		public BinaryFileNode AddNode(string name) {
			var retval = new BinaryFileNode() { Name = name };
			RootNode.Children.Add(retval);
			return retval;
		}


		public void Save(string filePath, string header) {

			bodyWriter.Seek(0, SeekOrigin.Begin);

			BeginAttributes();
			foreach (var attr in RootNode.Attributes) {
				WriteAttribute(attr.Key, attr.Value);
			}
			EndAttributes();

			Write((ushort)RootNode.Children.Count);
			foreach (var node in RootNode.Children)
				SaveNode(node);

			using (FileStream stream = File.Open(filePath, FileMode.Create)) {
				using (BinaryWriter writer = new BinaryWriter(stream)) {
					writer.Write(header);

					writer.Write((short)stringLookup.Count);

					foreach (string s in stringLookup) {
						writer.Write(s);
					}
					stream.Seek(0, SeekOrigin.End);

					tempValues.Seek(0, SeekOrigin.Begin);

					tempValues.CopyTo(stream);
				}

			}
		}

		private void SaveNode(BinaryFileNode node) {
			Write(node.Name);

			BeginAttributes();
			foreach (var attr in node.Attributes) {
				WriteAttribute(attr.Key, attr.Value);
			}
			EndAttributes();

			Write((ushort)node.Children.Count);
			foreach (var child in node.Children)
				SaveNode(child);
		}
	}
	public class BinaryFile {

		string[ ] textLookup;
		public BinaryFileNode RootNode;

		public T Decompile<T>() {
			return RootNode.Decompile<T>(false);
		}

		public static BinaryFileWriter Compile<T>(T mainObj) {

			Type rootType = typeof(T);

			BinaryFileWriter retval = new BinaryFileWriter();

			void CompileNode(BinaryFileNode rootNode, Type objType, object rootObj) {

				foreach (var field in objType.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
					object obj = field.GetValue(rootObj);
					CompileField(rootNode, field.Name, field.FieldType, obj, false);
				}
			}
			void CompileField(BinaryFileNode rootNode, string name, Type type, object obj, bool toChild) {
				BinaryFileNode child = null;

				Type objType = obj?.GetType()??type;

				switch (obj) {
					case sbyte b:
					case ushort s:
					case uint i:
					case ulong l:
						break;
					case bool b:
					case byte by:
					case short s:
					case int i:
					case long l:
					case float f:
					case double d:
					case Vector2 v2:
					case Vector3 v3:
					case Vector4 v4: 
						if (toChild) {

							child = rootNode.AddChild(name);
							child.Attributes.Add("value", obj);
						}
						else {
							rootNode.Attributes.Add(name, obj);
						}
					break;
					case string str:
						if (obj == null)
							break; 
						
						if (toChild) {

							child = rootNode.AddChild(name);
							child.Attributes.Add("value", obj);
						}
						else {
							rootNode.Attributes.Add(name, obj);
						}
						break;
					default:
						if (obj == null)
							break;

						if (objType.IsArray && (obj as Array).Length > 0) {
							var array = obj as Array;
							var arrayType = objType.GetElementType();

							int rank = objType.GetArrayRank();
							if (objType.GetArrayRank() > 1) {
								break;
							}

							if (arrayType == typeof(byte)) {
								if (toChild) {
									child = rootNode.AddChild(name);
									child.Attributes.Add("value", obj);
								}
								else {
									rootNode.Attributes.Add(name, obj);
								}
							}
							else {
								child = rootNode.AddChild(name);
								child.Attributes.Add("length", array.Length);

								int i = 0;
								switch (arrayType.FullName) {
									case "System.Boolean":
									case "System.Int64":
									case "System.Int32":
									case "System.Int16":
									case "System.Byte":
									case "System.String":
									case "System.Single":
									case "System.Double":
									case "System.Byte[]":
									case "Microsoft.Xna.Framework.Vector2":
									case "Microsoft.Xna.Framework.Vector3":
									case "Microsoft.Xna.Framework.Vector4":

										foreach (var item in array) {
											if (item != null) {
												child.Attributes.Add($"child{i}", item);
											}
											i++;
										}
										break;
									default:
										foreach (var item in array) {
											if (item != null) {
												var subChild = child.AddChild($"child{i}");

												CompileNode(subChild, arrayType, item);
											}
											i++;
										}
										break;
								}
							}
						}
						else {
							// List and dictionary
							if (objType.IsGenericType) {
								if (objType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {

									var keyType = objType.GetGenericArguments()[0];
									var valueType = objType.GetGenericArguments()[1];


									if (keyType == typeof(int) || keyType == typeof(string) || keyType == typeof(char)) {
										child = rootNode.AddChild(name);

										dynamic dict = obj;

										foreach (var item in dict.Keys) {

											CompileField(child, item.ToString(), valueType, dict[item], true);
										}
									}
								}
								else if (objType.GetGenericTypeDefinition() == typeof(List<>)) {

									throw new Exception();

									//var valueType = type.GetGenericArguments()[0];

									//BinaryFileNode child = rootNode.AddChild(name);

									//dynamic list = obj;

									//for (var i = 0; i < list.Count; i++) {
									//	CompileField(child, $"child{i}", valueType, list[i], false);
									//}
								}
							}
							else {
								child = rootNode.AddChild(name);

								CompileNode(child, objType, obj);
							}
						}
						break;
				}

				if (type == typeof(object)) {

					if (toChild && child != null) {
						child.Attributes[$"cst_{name}"] = objType.FullName;
					}
					else {
						rootNode.Attributes[$"cst_{name}"] = objType.FullName;
					}
				}
			}


			CompileNode(retval.RootNode, rootType, mainObj);

			return retval;
		}

		public string Header { get; private set; }

		public BinaryFile(string path) {
			BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));

			LoadData(reader);

			reader.Dispose();
		}
		public BinaryFile(Stream stream) {
			BinaryReader reader = new BinaryReader(stream);

			LoadData(reader);
		}

		private void LoadData(BinaryReader reader) {

			Header = reader.ReadString();

			textLookup = new string[reader.ReadUInt16()];

			for (int i = 0; i < textLookup.Length; ++i) {
				textLookup[i] = reader.ReadString();
			}

			RootNode = new BinaryFileNode();
			GetNode(RootNode, reader);

		}

		private void GetNode(BinaryFileNode node, BinaryReader reader) {

			int attrCount = reader.ReadByte();

			for (int j = 0; j < attrCount; ++j) {
				var val = reader.ReadUInt16();
				var attrName = textLookup[val];

				byte b = reader.ReadByte();
				object value = null;

				switch (b) {
					case BinaryFileWriter.BOOLEAN:
						value = reader.ReadBoolean();
						break;
					case BinaryFileWriter.BYTE:
						value = reader.ReadByte();
						break;
					case BinaryFileWriter.SHORT:
						value = reader.ReadInt16();
						break;
					case BinaryFileWriter.INT:
						value = reader.ReadInt32();
						break;
					case BinaryFileWriter.FLOAT:
						value = reader.ReadSingle();
						break;
					case BinaryFileWriter.LOOKUP_STRING:
						value = textLookup[reader.ReadInt16()];
						break;
					case BinaryFileWriter.DIRECT_STRING:
						value = reader.ReadString();
						break;
					case BinaryFileWriter.COMPRESSED_STRING:

						StringBuilder builder = new StringBuilder();
						short bytesCount = reader.ReadInt16();
						for (short ind = 0; ind < bytesCount; ind += 2) {
							byte repeatingCount = reader.ReadByte();
							char character = (char)reader.ReadByte(); // Direct cast
							builder.Append(character, repeatingCount);
						}
						value = builder.ToString();

						break;
					case BinaryFileWriter.LONG:
						value = reader.ReadInt64();
						break;
					case BinaryFileWriter.DOUBLE:
						value = reader.ReadDouble();
						break;
					case BinaryFileWriter.BYTE_ARRAY: {
						uint length = reader.ReadUInt32();
						var array = new byte[length];
						for (uint idx = 0; idx < length; ++idx) {
							array[idx] = reader.ReadByte();
						}
						value = array;
						break;
					}
					case BinaryFileWriter.BYTE_ARRAY_COMPRESSED: {

						List<byte> bytes = new List<byte>();
						int length = reader.ReadInt16();

						for (int ind = 0; ind < length; ind += 2) {
							byte repeatingCount = reader.ReadByte();
							byte asdf = reader.ReadByte();
							for (int idx = 0; idx < repeatingCount; ++idx) {
								bytes.Add(asdf);
							}
						}
						value = bytes.ToArray();
						break;
					}
					case BinaryFileWriter.ENTITY:
						value = reader.ReadUInt32();

						break;
					case BinaryFileWriter.VECTOR2:
						value = new Vector2(reader.ReadSingle(), reader.ReadSingle());

						break;
					case BinaryFileWriter.VECTOR3:
						value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

						break;
					case BinaryFileWriter.VECTOR4:
						value = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

						break;
					case BinaryFileWriter.QUATERNION:
						value = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

						break;
					case BinaryFileWriter.MATRIX:
						value = new Matrix(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

						break;
				}
				node.Attributes.Add(attrName, value);
			}

			node.Children.AddRange(GetNodes(reader));

		}

		private IEnumerable<BinaryFileNode> GetNodes(BinaryReader reader) {

			int count = reader.ReadUInt16();

			for (int i = 0; i < count; ++i) {

				int v = reader.ReadUInt16();
				BinaryFileNode retval = new BinaryFileNode {
					Name = textLookup[v]
				};

				GetNode(retval, reader);

				yield return retval;
			}

			yield break;
		}

	}
}
