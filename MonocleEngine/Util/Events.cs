using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace Monocle
{
    public class Events
    {
        private static Dictionary<string, EventData> eventList;

		public Events()
        {
            eventList = new Dictionary<string, EventData>();
            BuildEventsList();
        }

        #region Execute

        public static void RunEvent(string eventName) {
            if (eventList.ContainsKey(eventName)) {
                RunEvent(eventList[eventName]);
            }
        }
        private static void RunEvent(EventData events) {
            foreach (var ev in events.events) {
                if (ev.Item2 == null) {
                    ev.Item1.Invoke(null, null);
                }
                else {
                    if (Engine.NextScene.Tracker.Entities.ContainsKey(ev.Item2)) {
                        foreach (var ent in Engine.NextScene.Tracker.Entities[ev.Item2]) {
                            ev.Item1.Invoke(ent, null);
                        }
					}
					if (Engine.NextScene.Tracker.Components.ContainsKey(ev.Item2)) {
						foreach (var ent in Engine.NextScene.Tracker.Components[ev.Item2]) {
							ev.Item1.Invoke(ent, null);
						}
					}
				}
			}
		}

        #endregion

        #region Parse Commands

        private void BuildEventsList()
        {
            List<Type> processedTypes = new List<Type>();

            //Check Monocle for Commands
            foreach (var type in Assembly.GetCallingAssembly().GetTypes()) {
                if (processedTypes.Contains(type))
                    continue;
                processedTypes.Add(type);
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    ProcessMethod(method);
            }

            //Check the calling assembly for Commands
            foreach (var type in Assembly.GetEntryAssembly().GetTypes()) {
				if (processedTypes.Contains(type))
					continue;
				processedTypes.Add(type);
				foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    ProcessMethod(method);
            }

        }

        private void ProcessMethod(MethodInfo method)
        {
            EventAttribute attr = null;
            {
                var attrs = method.GetCustomAttributes(typeof(EventAttribute), false);
                if (attrs.Length > 0)
                    attr = attrs[0] as EventAttribute;
            }

            if (attr != null)
            {

                try {
                    EventData data;

					if (!eventList.TryGetValue(attr.Name, out data)) {
						data = new EventData();
                        eventList[attr.Name] = data;
                    }

                    if (method.IsStatic) {
                        data.events.Add((method, null));
				    }
				    else {
						data.events.Add((method, method.DeclaringType));
					}

                }
                catch {
                }

            }
        }

        #endregion

        private class EventData {
            public List<(MethodInfo, Type)> events = new List<(MethodInfo, Type)>();
		}
    }

    public class EventAttribute : Attribute
    {
        public string Name;

        public EventAttribute(string name)
        {
            Name = name;
        }
    }
}

