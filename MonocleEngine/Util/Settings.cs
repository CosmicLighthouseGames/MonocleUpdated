using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Newtonsoft.Json;
using System.Threading;

namespace Monocle {

	public static partial class Settings {
		public class HideSettingAttribute : Attribute { }

		const string SETTINGS_FILE = "settings.yaml";

		public static string LastLoadedLevel { get; set; } = "vanilla/debug";
		[HideSetting]
		public static bool Debug { get; set; }

		internal static void LoadSettings() {

			if (!File.Exists(SETTINGS_FILE)) {
				OnNewSettings();
				SaveSettings();
			}


			var des = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

			var data = des.Deserialize<Dictionary<string, object>>(File.ReadAllText(SETTINGS_FILE));

			foreach (var property in typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
				if (data.ContainsKey(property.Name)) {

					object reparse(Type typing, object oldValue) {

						if (typing == typeof(int)) {

							return int.Parse((string)oldValue);
						}
						else if (typing == typeof(bool)) {

							return bool.Parse((string)oldValue);
						}
						else if (typing.IsEnum) {
							return Enum.Parse(typing, (string)oldValue);
						}
						else if (typing.IsArray) {
							Type localType = typing.GetElementType();
							string fulltype = oldValue.GetType().Name;

							if (oldValue is List<object>) {
								var oldArray = (List<object>)oldValue;

								Array val = Array.CreateInstance(localType, oldArray.Count);
								for (int i = 0; i < val.Length; i++) {
									val.SetValue(reparse(localType, oldArray[i]), i);
								}

								return val;
							}
							else if (oldValue.GetType().IsArray) {
								var oldArray = (Array)oldValue;

								Array val = Array.CreateInstance(localType, oldArray.Length);
								for (int i = 0; i < val.Length; i++) {
									val.SetValue(reparse(localType, oldArray.GetValue(i)), i);
								}
							}
						}
						return oldValue;
					}

					var value = data[property.Name];
					if (value == null) {
						if (property.PropertyType == typeof(string)) {
							value = "";
						}
						else if (property.PropertyType.IsArray) {
							value = Array.CreateInstance(property.PropertyType.GetElementType(), 0);
						}
					}
					else if (value.GetType() != property.PropertyType) {
						value = reparse(property.PropertyType, value);
					}
					property.SetValue(null, value);

				}
			}

			OnLoadSettings();
		}
		internal static void SaveSettings() {

			var des = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();


			Dictionary<string, object> oldData;
			if (File.Exists(SETTINGS_FILE)) {
				oldData = des.Deserialize<Dictionary<string, object>>(File.ReadAllText(SETTINGS_FILE));
			}
			else {
				oldData = new Dictionary<string, object>();
			}

			var data = new Dictionary<string, object>();


			foreach (var property in typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
				if (oldData.ContainsKey(property.Name) || !(property.GetCustomAttributes(false).Length > 0 && property.GetCustomAttributes(false)[0] is HideSettingAttribute)) {
					var value = property.GetValue(null);
					data[property.Name] = value;

				}
			}

#if DEBUG
			data["Debug"] = true;
#endif


			var ser = new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

			File.WriteAllText(SETTINGS_FILE, ser.Serialize(data));
		}

		static partial void OnNewSettings();
		static partial void OnLoadSettings();
	}
}
