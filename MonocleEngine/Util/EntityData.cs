using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Monocle;

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;


namespace Monocle {

	public class EntityLevelIDAttribute : Attribute {

		public string IDValue;

		public EntityLevelIDAttribute(string value) {
			IDValue = value;
		}
	}
	public struct EntityData {

		static EntityData() {
			EntityIDs = new Dictionary<string, Type>();
			foreach (var type in Assembly.GetEntryAssembly().GetTypes()) {
				ProcessType(type);
			}
			foreach (var type in Assembly.GetCallingAssembly().GetTypes()) {
				ProcessType(type);
			}
		}
		static Dictionary<string, Type> EntityIDs;

		static void ProcessType(Type type) {
			EntityLevelIDAttribute id = type.GetCustomAttribute<EntityLevelIDAttribute>();

			if (id != null && !EntityIDs.ContainsKey(id.IDValue.ToLower())) {
				EntityIDs[id.IDValue.ToLower()] = type;
			}
			//foreach (var mi in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
			//	id = mi.GetCustomAttribute<EntityLevelIDAttribute>();
			//	if (id != null && !EntityMethods.ContainsKey(id.IDValue)) {
			//		EntityMethods.Add(id.IDValue, mi);
			//	}
			//}
		}

		public static Entity GetFromData(EntityData data) {

			if (EntityIDs.ContainsKey(data.name.ToLower())) {

				Type type = EntityIDs[data.name.ToLower()];
				if (type.IsSubclassOf(typeof(Entity))) {
#if CATCH_ERRORS
					try {

						var obj = Activator.CreateInstance(type, entData);
						Add(obj as Entity);
					}
					catch (Exception E) {//2064184520
						Engine.Commands.TempOpen = 3;
						Engine.Commands.Log(E, Color.Red);
					}
#else
					var obj = Activator.CreateInstance(type, data);
					return (obj as Entity);
#endif
				}
				return null;
			}
			else {
				return null;
			}
		}

		public static void ResetEntityDataID() { ID_Extra = 0; }

        private static uint ID_Extra;

        public string name;
        public string id;
        public bool constantEntity { get; internal set; }
        public Vector3 Position { get { return new Vector3(x, y, z); } }
        public int x, y, z, width, height, depth;
        public Dictionary<string, object> values;

        public static EntityData Default {
            get {
                EntityData ret = new EntityData();
                
                ret.values = new Dictionary<string, object>();
                ret.id = (ID_Extra++).ToString();
                ret.constantEntity = false;

                return ret;
            }
        }

        public static EntityData FromPosition(Vector3 position) {
            var retval = Default;

            retval.x = (int)position.X;
            retval.y = (int)position.Y;
            retval.z = (int)position.Z;

            return retval;
		}

        public int GetInt(string value, int defValue = 0) {
            return values.GetInt(value, defValue);
        }
        public string GetString(string value, string defValue = "") {
			return values.GetString(value, defValue);
		}
        public float GetFloat(string value, float defValue = 0) {
			return values.GetFloat(value, defValue);
		}
		public Vector2 GetVector(string value) {
			return values.GetVector(value);

		}
		public Vector2 GetVector(string value, Vector2 defValue) {
			return values.GetVector(value, defValue);

        }
        public bool GetBool(string value, bool defValue = false) {
			return values.GetBool(value, defValue);
		}
        public T GetEnum<T>(string value, T defValue = default) {
			return values.GetEnum(value, defValue);
		}

        public T[] GetArray<T>(string value) {
			return values.GetArray<T>(value);
		}
    }
}
