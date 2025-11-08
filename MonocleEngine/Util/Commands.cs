using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Monocle {
	public class Commands
    {
        private const float UNDERSCORE_TIME = .5f;
        private const float REPEAT_DELAY = .5f;
        private const float REPEAT_EVERY = 1 / 30f;
        private const float OPACITY = .8f;

        public static bool AllowDebugCommand = true, AllowExitCommand = true;

        public bool Enabled = true;
        public bool Open;

        public float TempOpen;
        public Action[] FunctionKeyActions { get; private set; }

        private Dictionary<string, CommandInfo> commands;
        private List<string> sorted;

        private KeyboardState oldState;
        private KeyboardState currentState;
        private string currentText = "";
        private List<Line> drawCommands;
        private bool underscore;
        private float underscoreCounter;
        private List<string> commandHistory;
        private int seekIndex = -1;
        private int tabIndex = -1;
        private string tabSearch;
        private float repeatCounter = 0;
        private Keys? repeatKey = null;
        private bool canOpen;

        SpriteBatch batch;
		Texture2D pixel;

        int highlightStart, highlightEnd;

		public Commands() {
			pixel = new Texture2D(Engine.Instance.GraphicsDevice, 1, 1);
			var colors = new Color[1];
			colors[0] = Color.White;
			pixel.SetData(colors);

			batch = new SpriteBatch(Engine.Graphics.GraphicsDevice);

            commandHistory = new List<string>();
            drawCommands = new List<Line>();
            commands = new Dictionary<string, CommandInfo>();
            sorted = new List<string>();
            FunctionKeyActions = new Action[12];

            BuildCommandsList();
        }

        public void Log(object obj, Color color)
        {
            string str = obj.ToString();

            //Newline splits
            if (str.Contains("\n"))
            {
                var all = str.Split('\n');
                foreach (var line in all)
                    Log(line, color);
                return;
            }

            //Split the string if you overlow horizontally
            float maxWidth = (Engine.UnitWidth * 2) - 1;
            float len = Draw.DefaultFont.MeasureString(str).X;
			while (len > maxWidth)
            {
                int split = -1;
                for (int i = 0; i < str.Length; i++)
                {
                    if (str[i] == ' ')
                    {
                        if (Draw.DefaultFont.MeasureString(str.Substring(0, i)).X <= maxWidth)
                            split = i;
                        else
                            break;
                    }
                }

                if (split == -1)
                    break;

                drawCommands.Insert(0, new Line(str.Substring(0, split), color));
                str = str.Substring(split + 1);
            }

            drawCommands.Insert(0, new Line(str, color));

            //Don't overflow top of window
            int maxCommands = (Engine.Instance.Window.ClientBounds.Height - 100) / 30;
            while (drawCommands.Count > maxCommands)
                drawCommands.RemoveAt(drawCommands.Count - 1);
        }

        public void Log(object obj)
        {
            Log(obj, Color.White);
        }

        #region Updating and Rendering

        internal void UpdateClosed()
        {
            if (!canOpen)
                canOpen = true;
            else if (MInput.Keyboard.Pressed(Keys.OemTilde))
            {
                Open = true;
                currentState = Keyboard.GetState();
            }

            for (int i = 0; i < FunctionKeyActions.Length; i++)
                if (MInput.Keyboard.Pressed((Keys)(Keys.F1 + i)))
                    ExecuteFunctionKeyAction(i);
        }

        internal void UpdateOpen()
        {
            if (TempOpen > 0) {

				if (MInput.Keyboard.Pressed(Keys.OemTilde)) {
					Open = true;
                    TempOpen = 0;
					currentState = Keyboard.GetState();
				}

				TempOpen = Calc.Approach(TempOpen, 0, Engine.DeltaTime);
                return;
			}
			oldState = currentState;
            currentState = Keyboard.GetState();

            underscoreCounter += Engine.DeltaTime;
            while (underscoreCounter >= UNDERSCORE_TIME)
            {
                underscoreCounter -= UNDERSCORE_TIME;
                underscore = !underscore;
            }

            if (repeatKey.HasValue)
            {
                if (currentState[repeatKey.Value] == KeyState.Down)
                {
                    repeatCounter += Engine.DeltaTime;

                    while (repeatCounter >= REPEAT_DELAY)
                    {
                        HandleKey(repeatKey.Value);
                        repeatCounter -= REPEAT_EVERY;
                    }
                }
                else
                    repeatKey = null;
            }

            foreach (Keys key in currentState.GetPressedKeys())
            {
                if (oldState[key] == KeyState.Up)
                {
                    HandleKey(key);
                    break;
                }
            }
		}
		[DllImport("user32.dll")]
		internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

		[DllImport("user32.dll")]
		internal static extern bool CloseClipboard();

		[DllImport("user32.dll")]
		internal static extern bool SetClipboardData(uint uFormat, IntPtr data);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetClipboardData(uint uFormat);

		[DllImport("Kernel32.dll", SetLastError = true)]
		private static extern IntPtr GlobalLock(IntPtr hMem);

		[DllImport("Kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalUnlock(IntPtr hMem);

		[DllImport("Kernel32.dll", SetLastError = true)]
		private static extern int GlobalSize(IntPtr hMem);


		private const uint CF_UNICODETEXT = 13U;

		private string GetClipboard() {

			try {
				if (!OpenClipboard(IntPtr.Zero))
					return null;

				IntPtr handle = GetClipboardData(CF_UNICODETEXT);
				if (handle == IntPtr.Zero)
					return null;

				IntPtr pointer = IntPtr.Zero;

				try {
					pointer = GlobalLock(handle);
					if (pointer == IntPtr.Zero)
						return null;

					int size = GlobalSize(handle);
					byte[] buff = new byte[size];

					Marshal.Copy(pointer, buff, 0, size);

					return Encoding.Unicode.GetString(buff).TrimEnd('\0');
				}
				finally {
					if (pointer != IntPtr.Zero)
						GlobalUnlock(handle);
				}
			}
			finally {
				CloseClipboard();
			}

			//OpenClipboard(IntPtr.Zero);
			//         var ptr = GetClipboardData(13);

			//CloseClipboard();

			//return "";
		}

		public static void SetClipboard(string value) {
			if (value == null)
				throw new ArgumentNullException("Attempt to set clipboard with null");

			Process clipboardExecutable = new Process();
			clipboardExecutable.StartInfo = new ProcessStartInfo // Creates the process
			{
				RedirectStandardInput = true,
                CreateNoWindow = true,
				FileName = @"clip",
			};
			clipboardExecutable.Start();

			clipboardExecutable.StandardInput.Write(value); // CLIP uses STDIN as input.
															// When we are done writing all the string, close it so clip doesn't wait and get stuck
			clipboardExecutable.StandardInput.Close();

            clipboardExecutable.WaitForExit();

			return;
		}


		private void HandleKey(Keys key)
        {
            if (key != Keys.Tab && key != Keys.LeftShift && key != Keys.RightShift && key != Keys.RightAlt && key != Keys.LeftAlt && key != Keys.RightControl && key != Keys.LeftControl)
                tabIndex = -1;

            if (key != Keys.OemTilde && key != Keys.Oem8 && key != Keys.Enter && repeatKey != key)
            {
                repeatKey = key;
                repeatCounter = 0;
            }

            bool pressControl = currentState[Keys.LeftControl] == KeyState.Down || currentState[Keys.RightControl] == KeyState.Down;
			bool pressShift = currentState[Keys.LeftShift] == KeyState.Down || currentState[Keys.RightShift] == KeyState.Down;


            void AddText(string text) {
                try {
					text = text.Replace("\n", "").Replace("\t", "");

					if (highlightStart == highlightEnd) {
						if (highlightStart == currentText.Length) {
							currentText += text;
						}
						else {
							string start = currentText.Substring(0, highlightStart);
							string end = currentText.Substring(highlightStart, currentText.Length - highlightStart);
							currentText = start + text + end;
						}
						highlightStart += text.Length;
					}
					else {
						int left = Math.Min(highlightEnd, highlightStart);
						int right = Math.Max(highlightEnd, highlightStart);

						string start = currentText.Substring(0, left);
						string end = currentText.Substring(right, currentText.Length - right);

						currentText = start + text + end;

						highlightStart = left + text.Length;
					}
					highlightEnd = highlightStart;
				}
                catch {

                }
			}

			void AddChar(char text) {
                AddText(text.ToString());
			}

			void DeleteHighlight() {
				int left = Math.Min(highlightEnd, highlightStart);
				int right = Math.Max(highlightEnd, highlightStart);

				string start = currentText.Substring(0, left);
				string end = currentText.Substring(right, currentText.Length - right);

				currentText = start + end;

				highlightStart = left;
				highlightEnd = highlightStart;

			}

			switch (key) {
				case Keys.A:
					if (pressControl) {
                        highlightStart = 0;
                        highlightEnd = currentText.Length;
					}
					else {
						goto default;
					}
					break;
				case Keys.X:
					if (pressControl) {
						if (highlightStart != highlightEnd) {
							int left = Math.Min(highlightEnd, highlightStart);
							int right = Math.Max(highlightEnd, highlightStart);

							string sub = currentText.Substring(left, right - left);
							SetClipboard(sub);
						}
                        goto case Keys.Back;
					}
					else {
						goto default;
					}
					break;
				case Keys.C:
					if (pressControl) {
                        if (highlightStart != highlightEnd) {
                            int left = Math.Min(highlightEnd, highlightStart);
                            int right = Math.Max(highlightEnd, highlightStart);

                            string sub = currentText.Substring(left, right - left);
							SetClipboard(sub);
						}
					}
					else {
						goto default;
					}
					break;
				case Keys.V:
                    if (pressControl) {
						AddText(GetClipboard());
                    }
                    else {
                        goto default;
                    }
                    break;
                default:
                    if (key.ToString().Length == 1)
                    {
                        if (pressShift)
                            AddText(key.ToString());
                        else
                            AddText(key.ToString().ToLower());
                    }
                    break;

                case (Keys.D1):
                    if (pressShift)
                        AddChar('!');
                    else
                        AddChar('1');
                    break;
                case (Keys.D2):
                    if (pressShift)
                        AddChar('@');
                    else
                        AddChar('2');
                    break;
                case (Keys.D3):
                    if (pressShift)
                        AddChar('#');
                    else
                        AddChar('3');
                    break;
                case (Keys.D4):
                    if (pressShift)
                        AddChar('$');
                    else
                        AddChar('4');
                    break;
                case (Keys.D5):
                    if (pressShift)
                        AddChar('%');
                    else
                        AddChar('5');
                    break;
                case (Keys.D6):
                    if (pressShift)
                        AddChar('^');
                    else
                        AddChar('6');
                    break;
                case (Keys.D7):
                    if (pressShift)
                        AddChar('&');
                    else
                        AddChar('7');
                    break;
                case (Keys.D8):
                    if (pressShift)
                        AddChar('*');
                    else
                        AddChar('8');
                    break;
                case (Keys.D9):
                    if (pressShift)
                        AddChar('(');
                    else
                        AddChar('9');
                    break;
                case (Keys.D0):
                    if (pressShift)
                        AddChar(')');
                    else
                        AddChar('0');
                    break;
                case (Keys.OemComma):
                    if (pressShift)
                        AddChar('<');
                    else
                        AddChar(',');
                    break;
                case Keys.OemPeriod:
                    if (pressShift)
                        AddChar('>');
                    else
                        AddChar('.');
                    break;
                case Keys.OemQuestion:
                    if (pressShift)
                        AddChar('?');
                    else
                        AddChar('/');
                    break;
                case Keys.OemSemicolon:
                    if (pressShift)
                        AddChar(':');
                    else
                        AddChar(';');
                    break;
                case Keys.OemQuotes:
                    if (pressShift)
                        AddChar('"');
                    else
						AddChar('\'');
                    break;
                case Keys.OemBackslash:
                    if (pressShift)
                        AddChar('|');
                    else
						AddChar('\\');
                    break;
                case Keys.OemOpenBrackets:
                    if (pressShift)
                        AddChar('{');
                    else
                        AddChar('[');
                    break;
                case Keys.OemCloseBrackets:
                    if (pressShift)
                        AddChar('}');
                    else
                        AddChar(']');
                    break;
                case Keys.OemMinus:
                    if (pressShift)
                        AddChar('_');
                    else
                        AddChar('-');
                    break;
                case Keys.OemPlus:
                    if (pressShift)
                        AddChar('+');
                    else
                        AddChar('=');
                    break;

                case Keys.Space:
                    AddText(" ");
                    break;
                case Keys.Back:
                    if (highlightStart != highlightEnd) {
                        DeleteHighlight();
					}
                    else {
						if (currentText.Length > 0 && highlightStart > 0) {


							highlightStart--;
							highlightEnd = highlightStart;

							string start = currentText.Substring(0, highlightStart);
							string end = currentText.Substring(highlightStart + 1, currentText.Length - (highlightStart + 1));

							currentText = start + end;
						}
					}
                    break;
                case Keys.Delete:
					if (highlightStart != highlightEnd) {
						DeleteHighlight();
					}
					else {
						if (currentText.Length > 0 && highlightStart < currentText.Length) {

							string start = currentText.Substring(0, highlightStart);
							string end = currentText.Substring(highlightStart + 1, currentText.Length - (highlightStart + 1));

							currentText = start + end;
						}
					}
					break;

                case Keys.Up:
					if (seekIndex < commandHistory.Count - 1)
                    {
                        seekIndex++;
                        currentText = string.Join(" ", commandHistory[seekIndex]);
					}
					highlightStart = currentText.Length;
					highlightEnd = highlightStart;
					break;
                case Keys.Down:
					if (seekIndex > -1)
                    {
                        seekIndex--;
                        if (seekIndex == -1) {
                            currentText = "";
                        }
                        else
                            currentText = string.Join(" ", commandHistory[seekIndex]);
					}
					highlightStart = currentText.Length;
					highlightEnd = highlightStart;
					break;
                case Keys.Left:
                    if (pressControl) {

                    }
                    else {
                        highlightStart = Math.Max(0, highlightStart - 1);
					}
					if (!pressShift) {
						highlightEnd = highlightStart;
					}
					break;
                case Keys.Right:
					if (pressControl) {

					}
					else {
						highlightStart = Math.Min(currentText.Length, highlightStart + 1);
					}
					if (!pressShift) {
						highlightEnd = highlightStart;
					}
					break;

				case Keys.Tab:
                    if (pressShift)
                    {
                        if (tabIndex == -1)
                        {
                            tabSearch = currentText;
                            FindLastTab();
                        }
                        else {

							bool foundCommand = false;
							if (Settings.Debug) {
								tabIndex--;
								foundCommand = true;
							}
							else {

								while (!foundCommand) {
									tabIndex--;
									if (tabIndex < 0)
										tabIndex = sorted.Count - 1;
                                    if (sorted[tabIndex] == "debug" && !AllowDebugCommand) { }
									else if (sorted[tabIndex] == "exit" && !AllowExitCommand) { }
									else if (!commands[sorted[tabIndex]].DebugOnly && sorted[tabIndex] != "debug")
										foundCommand = true;

								}
							}

                            if (tabIndex < 0 || (tabSearch != "" && sorted[tabIndex].IndexOf(tabSearch) != 0))
                                FindLastTab();
                        }
                    }
                    else
                    {
                        if (tabIndex == -1)
                        {
                            tabSearch = currentText;
                            FindFirstTab();
                        }
                        else {
							bool foundCommand = false;
							if (Settings.Debug) {
								tabIndex++;
                                foundCommand = true;
							}
                            else {
                                
								while (!foundCommand) {
                                    tabIndex++;
                                    if (tabIndex >= sorted.Count)
                                        tabIndex = 0;
									if (sorted[tabIndex] == "debug" && !AllowDebugCommand) { }
									else if (sorted[tabIndex] == "exit" && !AllowExitCommand) { }
									else if (!commands[sorted[tabIndex]].DebugOnly && sorted[tabIndex] != "debug")
                                        foundCommand = true;
                                    
                                }
                            }

                            if (tabIndex >= sorted.Count || (tabSearch != "" && sorted[tabIndex].IndexOf(tabSearch) != 0))
                                FindFirstTab();
                        }
                    }
                    if (tabIndex != -1)
                        currentText = sorted[tabIndex];

                    highlightEnd = currentText.Length;
                    highlightStart = highlightEnd;
                    break;

                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                    ExecuteFunctionKeyAction((int)(key - Keys.F1));
                    break;

                case Keys.Enter:
                    if (currentText.Length > 0)
                        EnterCommand();
                    break;

                case Keys.Oem8:
                case Keys.OemTilde:
                    Open = canOpen = false;
                    break;
            }
        }

        private void EnterCommand()
        {
            string[] data = currentText.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (commandHistory.Count == 0 || commandHistory[0] != currentText)
                commandHistory.Insert(0, currentText);
            drawCommands.Insert(0, new Line(currentText, Color.Aqua));
            currentText = "";
			highlightStart = 0;
			highlightEnd = 0;
			seekIndex = -1;

            string[] args = new string[data.Length - 1];
            for (int i = 1; i < data.Length; i++)
                args[i - 1] = data[i];
            ExecuteCommand(data[0].ToLower(), args);
        }

        private void FindFirstTab()
        {
            for (int i = 0; i < sorted.Count; i++) {
				if (sorted[i] == "debug" && !AllowDebugCommand) { }
				else if (sorted[i] == "exit" && !AllowExitCommand) { }
				else if ((tabSearch == "" || sorted[i].IndexOf(tabSearch) == 0) &&  (Settings.Debug || (!commands[sorted[i]].DebugOnly && sorted[i] != "debug")))
                {
                    tabIndex = i;
                    break;
                }
            }
        }

        private void FindLastTab()
        {
            for (int i = 0; i < sorted.Count; i++)
                if (tabSearch == "" || sorted[i].IndexOf(tabSearch) == 0)
                    tabIndex = i;
        }

        internal void Render()
        {
            //return;
            float screenWidth = Engine.WindowWidth / Engine.PixelsPerUnit;
			float screenHeight = Engine.WindowHeight / Engine.PixelsPerUnit;
            Draw.ClearGraphics(screenWidth, screenHeight);

            float fontHeight = Draw.DefaultFont.MeasureString("A").X + 1.25f;

			StringBuilder sb;

            //batch.Begin();

            //batch.Draw(pixel, new Rectangle(10, screenHeight - 50, screenWidth - 20, 40), Color.Black * OPACITY);

            Draw.Rect(0.5f, 0.5f, screenWidth - 1f, fontHeight, Color.Black * OPACITY);

			if (highlightStart != highlightEnd) {

				int left = Math.Min(highlightEnd, highlightStart);
				int right = Math.Max(highlightEnd, highlightStart);

				string start = currentText.Substring(0, left);
				string middle = currentText.Substring(left, right - left);
				string end = currentText.Substring(right, currentText.Length - right);

                float startLen = Draw.DefaultFont.MeasurePartialString(">" + currentText, left + 1).X;
				float middleLen = Draw.DefaultFont.MeasurePartialString(">" + currentText, right + 1).X;

				Draw.Rect(1f + startLen, 0.5f, middleLen - startLen, fontHeight, Color.White);

				Draw.DefaultFont.Draw(">" + start, new Vector2(1, .75f), Vector2.Zero, Vector2.One, Color.White);
				Draw.DefaultFont.Draw(middle, new Vector2(1 + startLen, .75f), Vector2.Zero, Vector2.One, Color.Black);
				Draw.DefaultFont.Draw(end, new Vector2(1 + middleLen, .75f), Vector2.Zero, Vector2.One, Color.White);

			}
            else {
				Draw.DefaultFont.Draw(">" + currentText, new Vector2(1, .75f), Vector2.Zero, Vector2.One, Color.White);
				if (underscore) {
                    float offset = Draw.DefaultFont.MeasurePartialString($">{currentText} ", highlightStart + 1).X;
					Draw.DefaultFont.Draw("|", new Vector2(1 + offset, .75f), Vector2.Zero, Vector2.One, Color.White);
				}
			}

            if (drawCommands.Count > 0) {
                float height = 1 + (fontHeight * drawCommands.Count);
                Draw.Rect(0.5f, fontHeight + 1.2f, screenWidth - 1, fontHeight * drawCommands.Count, Color.Black * OPACITY);
                for (int i = 0; i < drawCommands.Count; i++)
                    Draw.DefaultFont.Draw(drawCommands[i].Text, new Vector2(1, 3.5f + (fontHeight * i)), Vector2.Zero, Vector2.One, drawCommands[i].Color);
            }

            Draw.FallbackDepthState = DepthStencilState.None;
            Draw.GraphicsDevice.RasterizerState = RasterizerState.CullNone;
//            Draw.GraphicsDevice.DepthStencilState = DepthStencilState.None;
            Draw.RenderPass();
            Draw.ClearGraphics();

            //batch.End();
        }

        #endregion

        #region Execute

        public void ExecuteCommand(string command, string[] args) {
			if (command == "debug" && !AllowDebugCommand) {
				Log("Command '" + command + "' not found! Type 'help' for list of commands", Color.Yellow);
			}
			else if (command == "exit" && !AllowExitCommand) {
				Log("Command '" + command + "' not found! Type 'help' for list of commands", Color.Yellow);
			}
			else if (commands.ContainsKey(command) && (Settings.Debug || !commands[command].DebugOnly))
                commands[command].Action(args);
            else
                Log("Command '" + command + "' not found! Type 'help' for list of commands", Color.Yellow);
        }

        public void ExecuteFunctionKeyAction(int num)
        {
            if (FunctionKeyActions[num] != null)
                FunctionKeyActions[num]();
        }

        #endregion

        #region Parse Commands

        private void BuildCommandsList()
        {
#if !CONSOLE
            //Check Monocle for Commands
            foreach (var type in Assembly.GetCallingAssembly().GetTypes())
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    ProcessMethod(method);

            //Check the calling assembly for Commands
            foreach (var type in Assembly.GetEntryAssembly().GetTypes())
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    ProcessMethod(method);

            //Maintain the sorted command list
            foreach (var command in commands)
                sorted.Add(command.Key);
            sorted.Sort();
#endif
        }

        private void ProcessMethod(MethodInfo method)
        {
            Command attr = null;
            {
                var attrs = method.GetCustomAttributes(typeof(Command), false);
                if (attrs.Length > 0)
                    attr = attrs[0] as Command;
            }

            if (attr != null)
            {
                if (!method.IsStatic)
                    throw new Exception(method.DeclaringType.Name + "." + method.Name + " is marked as a command, but is not static");
                else
                {
                    CommandInfo info = new CommandInfo();
                    info.Help = attr.Help;
                    info.DebugOnly = attr.DebugOnly;

                    var parameters = method.GetParameters();
                    var defaults = new object[parameters.Length];                 
                    string[] usage = new string[parameters.Length];
                    
                    for (int i = 0; i < parameters.Length; i++)
                    {                       
                        var p = parameters[i];
                        usage[i] = p.Name + ":";

                        if (p.ParameterType == typeof(string))
                            usage[i] += "string";
                        else if (p.ParameterType == typeof(int))
                            usage[i] += "int";
                        else if (p.ParameterType == typeof(float))
                            usage[i] += "float";
                        else if (p.ParameterType == typeof(bool))
                            usage[i] += "bool";
                        else
                            throw new Exception(method.DeclaringType.Name + "." + method.Name + " is marked as a command, but has an invalid parameter type. Allowed types are: string, int, float, and bool");

                        if (p.DefaultValue == DBNull.Value)
                            defaults[i] = null;
                        else if (p.DefaultValue != null)
                        {
                            defaults[i] = p.DefaultValue;
                            if (p.ParameterType == typeof(string))
                                usage[i] += "=\"" + p.DefaultValue + "\"";
                            else
                                usage[i] += "=" + p.DefaultValue;
                        }
                        else
                            defaults[i] = null;
                    }

                    if (usage.Length == 0)
                        info.Usage = "";
                    else
                        info.Usage = "[" + string.Join(" ", usage) + "]";

                    info.Action = (args) =>
                        {
                            if (parameters.Length == 0)
                                InvokeMethod(method);
                            else
                            {
                                object[] param = (object[])defaults.Clone();

                                for (int i = 0; i < param.Length && i < args.Length; i++)
                                {
                                    if (parameters[i].ParameterType == typeof(string))
                                        param[i] = ArgString(args[i]);
                                    else if (parameters[i].ParameterType == typeof(int))
                                        param[i] = ArgInt(args[i]);
                                    else if (parameters[i].ParameterType == typeof(float))
                                        param[i] = ArgFloat(args[i]);
                                    else if (parameters[i].ParameterType == typeof(bool))
                                        param[i] = ArgBool(args[i]);
                                }

                                InvokeMethod(method, param);
                            }
                        };

                    commands[attr.Name] = info;
                }
            }
        }

        private void InvokeMethod(MethodInfo method, object[] param = null)
        {
            try
            {
                method.Invoke(null, param);
            }
            catch (Exception e)
            {
                Engine.Commands.Log(e.InnerException.Message, Color.Yellow);
                LogStackTrace(e.InnerException.StackTrace);
            }
        }

        private void LogStackTrace(string stackTrace)
        {
            foreach (var call in stackTrace.Split('\n'))
            {
                string log = call;

                //Remove File Path
                {
                    var from = log.LastIndexOf(" in ") + 4;
                    var to = log.LastIndexOf('\\') + 1;
                    if (from != -1 && to != -1)
                        log = log.Substring(0, from) + log.Substring(to);
                }

                //Remove arguments list
                {
                    var from = log.IndexOf('(') + 1;
                    var to = log.IndexOf(')');
                    if (from != -1 && to != -1)
                        log = log.Substring(0, from) + log.Substring(to);
                }

                //Space out the colon line number
                var colon = log.LastIndexOf(':');
                if (colon != -1)
                    log = log.Insert(colon + 1, " ").Insert(colon, " ");

                log = log.TrimStart();
                log = "-> " + log;

                Engine.Commands.Log(log, Color.White);
            }
        }

        private struct CommandInfo
        {
            public Action<string[]> Action;         
            public string Help;
            public string Usage;
            public bool DebugOnly;
        }

        #region Parsing Arguments

        private static string ArgString(string arg)
        {
            if (arg == null)
                return "";
            else
                return arg;
        }

        private static bool ArgBool(string arg)
        {
            if (arg != null)
                return !(arg == "0" || arg.ToLower() == "false" || arg.ToLower() == "f");
            else
                return false;
        }

        private static int ArgInt(string arg)
        {
            try
            {
                return Convert.ToInt32(arg);
            }
            catch
            {
                return 0;
            }
        }

        private static float ArgFloat(string arg)
        {
            try
            {
                return Convert.ToSingle(arg);
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #endregion

        #region Built-In Commands
#if !CONSOLE
        [Command("clear", "Clears the terminal", false)]
        public static void Clear()
        {
            Engine.Commands.drawCommands.Clear();
		}

        [Command("debug", "Enables debug", false)]
		public static void Debug(bool enabled = true) {
            Settings.Debug = enabled;
		}

		[Command("exit", "Exits the game", false)]
        private static void Exit()
        {
            Engine.Instance.Exit();
		}

		[Command("vsync", "Enables or disables vertical sync", false)]
        private static void Vsync(bool enabled = true)
        {
            Engine.Graphics.SynchronizeWithVerticalRetrace = enabled;
            Engine.Graphics.ApplyChanges();
            Engine.Commands.Log("Vertical Sync " + (enabled ? "Enabled" : "Disabled"));
        }

        [Command("fixed", "Enables or disables fixed time step", false)]
        private static void Fixed(bool enabled = true)
        {
            Engine.Instance.IsFixedTimeStep = enabled;
            Engine.Commands.Log("Fixed Time Step " + (enabled ? "Enabled" : "Disabled"));
        }

        [Command("framerate", "Sets the target framerate", false)]
        private static void Framerate(float target)
        {
            Engine.Instance.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / target);
        }

        [Command("count", "Logs amount of Entities in the Scene. Pass a tagIndex to count only Entities with that tag")]
        private static void Count(int tagIndex = -1)
        {
            if (Engine.CurrentScene == null)
            {
                Engine.Commands.Log("Current Scene is null!");
                return;
            }

            if (tagIndex < 0)
                Engine.Commands.Log(Engine.CurrentScene.Entities.Count.ToString());
            else
                Engine.Commands.Log(Engine.CurrentScene.TagLists[tagIndex].Count.ToString());
        }

        [Command("tracker", "Logs all tracked objects in the scene. Set mode to 'e' for just entities, 'c' for just components, or 'cc' for just collidable components")]
        private static void Tracker(string mode)
        {
            if (Engine.CurrentScene == null)
            {
                Engine.Commands.Log("Current Scene is null!");
                return;
            }

            switch (mode)
            {
                default:
                    Engine.Commands.Log("-- Entities --");
                    Engine.CurrentScene.Tracker.LogEntities();
                    Engine.Commands.Log("-- Components --");
                    Engine.CurrentScene.Tracker.LogComponents();
                    break;

                case "e":
                    Engine.CurrentScene.Tracker.LogEntities();
                    break;

                case "c":
                    Engine.CurrentScene.Tracker.LogComponents();
                    break;
            }
        }

        [Command("pooler", "Logs the pooled Entity counts")]
        private static void Pooler()
        {
            Engine.Pooler.Log();
        }

        [Command("fullscreen", "Switches to fullscreen mode", false)]
        private static void Fullscreen()
        {
            Engine.SetFullscreen();
        }

        [Command("window", "Switches to window mode", false)]
        private static void Window(int scale = 1)
        {
            Engine.SetWindowed((int)(Engine.UnitWidth * scale), (int)(Engine.UnitHeight * scale));
        }

        [Command("help", "Shows usage help for a given command", false)]
        private static void Help(string command)
        {
            if (Engine.Commands.sorted.Contains(command))
            {
                var c = Engine.Commands.commands[command];
                StringBuilder str = new StringBuilder();

                //Title
                str.Append(":: ");
                str.Append(command);

                //Usage
                if (!string.IsNullOrEmpty(c.Usage))
                {
                    str.Append(" ");
                    str.Append(c.Usage);
                }
                Engine.Commands.Log(str.ToString());
               
                //Help
                if (string.IsNullOrEmpty(c.Help))
                    Engine.Commands.Log("No help info set");
                else
                    Engine.Commands.Log(c.Help);
            }
            else
            {
                StringBuilder str = new StringBuilder();
                str.Append("Commands list: ");
                str.Append(string.Join(", ", Engine.Commands.sorted));
                Engine.Commands.Log(str.ToString());
                Engine.Commands.Log("Type 'help command' for more info on that command!");
            }
        }
#endif
#endregion

        private struct Line
        {
            public string Text;
            public Color Color;

            public Line(string text)
            {
                Text = text;
                Color = Color.White;
            }

            public Line(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }
    }

    public class Command : Attribute
    {
        public string Name;
        public string Help;
        public bool DebugOnly;

        public Command(string name, string help, bool debugOnly = true)
        {
            Name = name;
            Help = help;
            DebugOnly = debugOnly;
        }
    }
}

