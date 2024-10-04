using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monocle {
#if DEBUG
	static class EpicStopwatch {

		public static int reloadTime = 180;
		static Stopwatch main, secondary;

		static string secondaryKey;

		static Dictionary<string, float> data = new Dictionary<string, float>();


		public static void StartMain() {
			reloadTime--;
			main = Stopwatch.StartNew();
			data.Clear();
		}
		public static void StopMain(float threashold) {
			main.Stop();

			if (threashold > 0 && reloadTime <= 0 && main.Elapsed.TotalMilliseconds > threashold) {
				reloadTime = 180;
			}
		}

		public static void StartSecondary(string key) {
			secondary = Stopwatch.StartNew();
			if (!data.ContainsKey(key)) {
				data[key] = 0;
			}
			secondaryKey = key;
		}
		public static void StopSecondary() {
			secondary.Stop();
			data[secondaryKey] += (float)secondary.Elapsed.TotalMilliseconds;
		}
	}

#endif
}
