

using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Monocle {
	//public class LDtkLevel {
	//	public class Header {
	//		public string fileType, schema, appVersion;
	//	}
	//	public class Field {
	//		public string __identifier, __type;
	//		public object __value;


	//		public object GetValue() {
	//			switch (__value) {
	//				case JArray ja:
	//					return ja.ToObject<string[]>();
	//				default:
	//					return __value;
	//			}
	//		}
	//	}
	//	public class Layer {
	//		public string __identifier, __type;
	//		public int __cWid, __cHei, __gridSize;
	//		public float __opacity;
	//		public int __pxTotalOffsetX, __pxTotalOffsetY;
	//		public string iid;
	//		public int levelId;
	//		public int layerDefUid;
	//		public int pxOffsetX, pxOffsetY;
	//		public bool visible;

	//		public int[] intGridCsv;
	//		public Entity[] entityInstances;
	//		public int seed;
	//	}
	//	public class Entity {
	//		public string __identifier, __type;
	//		public int[] __grid, px;
	//		public float[] __pivot;

	//		public string iid;
	//		public int width, height, defUid;

	//		public Field[] fieldInstances;

	//		public Layer layer;

	//		public EntityData ToData() {
	//			var retval = new EntityData();

	//			retval.values = new Dictionary<string, object>();
	//			retval.constantEntity = true;


	//			retval.x = px[0] / Engine.PixelsPerUnit;
	//			retval.y = px[1] / Engine.PixelsPerUnit;

	//			retval.id = iid;

	//			retval.width = width / Engine.PixelsPerUnit;
	//			retval.height = height / Engine.PixelsPerUnit;
	//			retval.name = __identifier;

	//			float __gridSize = 8;//layer.__gridSize;

	//			if (fieldInstances != null) {
	//				foreach (var p in fieldInstances) {
	//					object val;

	//					object getValue(string t, object v) {
	//						object retval;

	//						switch (t) {
	//							case "Int":
	//								retval = (int)(long)v;
	//								break;
	//							case "Float":
	//								retval = v is long ? (float)(long)v : (float)(double)v;
	//								break;
	//							case "Bool":
	//								retval = (bool)v;
	//								break;
	//							case "Point":
	//								retval = 0;

	//								if (v == null)
	//									retval = null;
	//								else {
	//									var obj = v as JObject;
	//									var item = obj.First;

	//									var p = obj["cx"].ToString();

	//									retval = new Vector2(int.Parse(obj["cx"].ToString()) * __gridSize, int.Parse(obj["cy"].ToString()) * __gridSize) / Engine.PixelsPerUnit;
	//								}

	//								break;
	//							case "String":
	//							case "FilePath":
	//							case "Color":
	//								retval = (string)v;
	//								break;
	//							case "EntityRef":
	//								if (v == null)
	//									retval = null;
	//								else {
	//									var obj = v as JObject;
	//									retval = Regex.Match(obj.First.ToString(), @"""entityIid"": ""([a-fA-F0-9\-]+)""").Groups[1].Value;
	//								}
	//								break;
	//							default:
	//								retval = v;
	//								break;
	//						}

	//						return retval;
	//					}
	//					if (p.__type.StartsWith("Array<")) {
	//						string type = p.__type[6..^1];

	//						var array = new List<object>();

	//						foreach (var item in p.__value as JArray) {
	//							array.Add(getValue(type, item.ToObject(typeof(object))));
	//						}

	//						switch (type) {
	//							case "Int":
	//								val = array.Select(x => (int)x).ToArray();
	//								break;
	//							case "Float":
	//								val = array.Select(x => (float)x).ToArray();
	//								break;
	//							case "Bool":
	//								val = array.Select(x => (bool)x).ToArray();
	//								break;
	//							case "Point":
	//								val = array.Select(x => (Vector2)x).ToArray();
	//								break;
	//							case "String":
	//							case "FilePath":
	//							case "Color":
	//							case "EntityRef":
	//								val = array.Select(x => (string)x).ToArray();
	//								break;
	//							default:
	//								throw new Exception();
	//						}
	//					}
	//					else {
	//						val = getValue(p.__type, p.__value);
	//					}

	//					if (p.__identifier == "Y") {
	//						retval.y = (int)val;
	//					}
	//					else {
	//						retval.values.Add(p.__identifier, val);
	//					}
	//				}
	//			}

	//			return retval;
	//		}

	//		public override string ToString() {
	//			return __identifier;
	//		}
	//	}

	//	public Header __header__;
	//	public string identifier, iid;
	//	public int uid;
	//	public int worldX, worldY;
	//	public int worldDepth;
	//	public int pxWid, pxHei;
	//	public string __bgColor;
	//	public string bgColor;

	//	public Field[] fieldInstances;
	//	public Layer[] layerInstances;

	//	public Layer GetLayer(string name) {
	//		foreach (var layer in layerInstances) {
	//			if (layer.__identifier == name)
	//				return layer;
	//		}

	//		return null;
	//	}
	//}
}