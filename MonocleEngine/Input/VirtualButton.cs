using Microsoft.Xna.Framework.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using static Monocle.MInput;

namespace Monocle
{
	public enum MonocleJoyButton {

		None = 0,
		PadLeft = Buttons.DPadLeft, PadRight = Buttons.DPadRight, PadUp = Buttons.DPadUp, PadDown = Buttons.DPadDown,

		ShoulderAny, TriggerAny = 100,
		ShoulderLeft = Buttons.LeftShoulder, ShoulderRight = Buttons.RightShoulder,
		TriggerLeft = Buttons.LeftTrigger, TriggerRight = Buttons.RightTrigger,

		OptionsA = Buttons.Start, OptionsB = Buttons.Back,

		LeftJoystickLeft, LeftJoystickRight, LeftJoystickUp, LeftJoystickDown, LeftJoystickPress = Buttons.LeftStick,
		RightJoystickLeft, RightJoystickRight, RightJoystickUp, RightJoystickDown, RightJoystickPress = Buttons.RightStick,
		ButtonLeft, ButtonRight, ButtonUp, ButtonDown,

		TypicalLeft, TypicalRight, TypicalUp, TypicalDown,
		JoystickLeft, JoystickRight, JoystickUp, JoystickDown,
		AnyLeft, AnyRight, AnyUp, AnyDown,
	}
	/// <summary>
	/// A virtual input that is represented as a boolean. As well as simply checking the current button state, you can ask whether it was just pressed or released this frame. You can also keep the button press stored in a buffer for a limited time, or until it is consumed by calling ConsumeBuffer()
	/// </summary>
	public class VirtualButton : VirtualInput
	{
		public List<Node> Nodes;
		public Node CutsceneNode;
		public bool UseCutscene;
		public float BufferTime;
		public bool Repeating { get; private set; }

		private float firstRepeatTime;
		private float multiRepeatTime;
		private float bufferCounter;
		private float repeatCounter;
		private bool canRepeat;
		private bool consumed;
		private bool inCutscene, wasInCutscene;

		public VirtualButton(float bufferTime)
			: base()
		{
			Nodes = new List<Node>();
			BufferTime = bufferTime;
		}

		public VirtualButton()
			: this(0)
		{

		}

		public VirtualButton(float bufferTime, params Node[] nodes)
			: base()
		{
			Nodes = new List<Node>(nodes);
			BufferTime = bufferTime;
		}

		public VirtualButton(params Node[] nodes)
			: this(0, nodes)
		{

		}

		public void SetRepeat(float repeatTime)
		{
			SetRepeat(repeatTime, repeatTime);
		}
		
		public void SetRepeat(float firstRepeatTime, float multiRepeatTime)
		{
			this.firstRepeatTime = firstRepeatTime;
			this.multiRepeatTime = multiRepeatTime;
			canRepeat = (this.firstRepeatTime > 0);
			if (!canRepeat)
				Repeating = false;
		}

		public override void Update()
		{
			consumed = false;

			bufferCounter -= Engine.DeltaTime;

			wasInCutscene = inCutscene;
			inCutscene = UseCutscene;

			bool check = false;
			CutsceneNode?.Update();


			if (UseCutscene) {

				if (CutsceneNode?.Pressed??false) {
					bufferCounter = BufferTime;
					check = true;
				}
			}
			else {

				foreach (var node in Nodes) {
					node.Update();
					if (node.Pressed) {
						bufferCounter = BufferTime;
						check = true;
					}
					else if (node.Check)
						check = true;
				}
			}

			if (!check)
			{
				Repeating = false;
				repeatCounter = 0;
				bufferCounter = 0;
			}
			else if (canRepeat)
			{
				Repeating = false;
				if (repeatCounter == 0)
					repeatCounter = firstRepeatTime;
				else
				{
					repeatCounter -= Engine.DeltaTime;
					if (repeatCounter <= 0)
					{
						Repeating = true;
						repeatCounter = multiRepeatTime;
					}
				}
			}
		}

		public bool Check
		{
			get
			{
				if (MInput.Disabled)
					return false;

				if (UseCutscene)
					return CutsceneNode.Check;
				else
					foreach (var node in Nodes)
						if (node.Check)
							return true;
				return false;
			}
		}

		public bool Pressed
		{
			get
			{
				if (MInput.Disabled)
					return false;

				bool nodeCheck = false;

				foreach (var node in Nodes)
					if (node.Check)
						nodeCheck = true;

				if (UseCutscene && !wasInCutscene) {
					if (CutsceneNode.Check && !nodeCheck && !consumed)
						return true;
				}
				//if (wasInCutscene && !UseCutscene) {
				//	if (!CutsceneNode.Check && nodeCheck && !consumed)
				//		return true;
				//}

				if (consumed)
					return false;

				if (bufferCounter > 0 || Repeating)
					return true;

				if (UseCutscene)
					return CutsceneNode.Pressed;
				else
					foreach (var node in Nodes)
						if (node.Pressed)
							return true;
				return false;
			}
		}

		public bool Released
		{
			get
			{
				if (MInput.Disabled)
					return false;

				bool nodeCheck = false;

				foreach (var node in Nodes)
					if (node.Check)
						nodeCheck = true;

				if (UseCutscene && !wasInCutscene) {
					if (!CutsceneNode.Check && nodeCheck)
						return true;
				}
				if (wasInCutscene && !UseCutscene) {
					if (CutsceneNode.Check && !nodeCheck)
						return true;
				}

				if (UseCutscene)
					return CutsceneNode.Released;
				else
					foreach (var node in Nodes)
						if (node.Released)
							return true;
				return false;
			}
		}

		/// <summary>
		/// Ends the Press buffer for this button
		/// </summary>
		public void ConsumeBuffer()
		{
			bufferCounter = 0;
		}

		/// <summary>
		/// This button will not register a Press for the rest of the current frame, but otherwise continues to function normally. If the player continues to hold the button, next frame will not count as a Press. Also ends the Press buffer for this button
		/// </summary>
		public void ConsumePress()
		{
			bufferCounter = 0;
			consumed = true;
		}

		public static implicit operator bool(VirtualButton button)
		{
			return button.Check;
		}

		public abstract class Node : VirtualInputNode
		{
			public abstract bool Check { get; }
			public abstract bool Pressed { get; }
			public abstract bool Released { get; }
		}

		public class KeyboardKey : Node
		{
			public Keys Key;

			public KeyboardKey(Keys key)
			{
				Key = key;
			}

			public override bool Check
			{
				get { return MInput.Keyboard.Check(Key); }
			}

			public override bool Pressed
			{
				get { return MInput.Keyboard.Pressed(Key); }
			}

			public override bool Released
			{
				get { return MInput.Keyboard.Released(Key); }
			}
		}

		public class PadButton : Node
		{
			public int GamepadIndex;
			public Buttons Button;

			

			public PadButton(int gamepadIndex, Buttons button)
			{
				GamepadIndex = gamepadIndex;
				Button = button;
			}

			public override bool Check
			{
				get {
					switch (Button) {
						default:
							return MInput.GamePads[GamepadIndex].Check((Buttons)Button);
							break;
					}
					return false;

				}
			}

			public override bool Pressed {
				get {
					switch (Button) {
						default:
							return MInput.GamePads[GamepadIndex].Pressed((Buttons)Button);
							break;
					}
					return false;

				}
			}

			public override bool Released {
				get {
					switch (Button) {
						default:
							return MInput.GamePads[GamepadIndex].Released(Button);
							break;
					}
					return false;

				}
			}
		}

		#region Pad Stick

		public class PadLeftStick : Node
		{
			public int GamepadIndex;
			public float Deadzone;
			public GamePadDirection Direction;
			
			public PadLeftStick(int gamepadindex, float deadzone, GamePadDirection direction) {
				GamepadIndex = gamepadindex;
				Deadzone = deadzone;
				Direction = direction;
			}

			public override bool Check
			{
				get {
					return GamePads[GamepadIndex].GetLeftStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true);
				}
			}

			public override bool Pressed
			{
				get { return 
						GamePads[GamepadIndex].GetLeftStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true) &&
						!GamePads[GamepadIndex].GetLeftStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, false); }
			}

			public override bool Released {
				get {
					return
						!GamePads[GamepadIndex].GetLeftStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true) &&
						GamePads[GamepadIndex].GetLeftStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, false);
				}
			}
		}

		public class PadRightStick : Node
		{
			public int GamepadIndex;
			public float Deadzone;
			public MInput.GamePadDirection Direction;

			public PadRightStick(int gamepadindex, float deadzone, MInput.GamePadDirection direction) {
				GamepadIndex = gamepadindex;
				Deadzone = deadzone;
				Direction = direction;
			}
			public override bool Check {
				get {
					return GamePads[GamepadIndex].GetRightStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true);
				}
			}

			public override bool Pressed {
				get {
					return
						GamePads[GamepadIndex].GetRightStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true) &&
						!GamePads[GamepadIndex].GetRightStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, false);
				}
			}

			public override bool Released {
				get {
					return
						!GamePads[GamepadIndex].GetRightStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, true) &&
						GamePads[GamepadIndex].GetRightStickDirection(Direction, GamePadData.JOYSTICK_THREASHOLD, Deadzone, false);
				}
			}
		}

		#endregion

		#region Pad Triggers

		public class PadLeftTrigger : Node
		{
			public int GamepadIndex;
			public float Threshold;

			public PadLeftTrigger(int gamepadIndex, float threshold)
			{
				GamepadIndex = gamepadIndex;
				Threshold = threshold;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].LeftTriggerCheck(Threshold); }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].LeftTriggerPressed(Threshold); }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].LeftTriggerReleased(Threshold); }
			}
		}

		public class PadRightTrigger : Node
		{
			public int GamepadIndex;
			public float Threshold;

			public PadRightTrigger(int gamepadIndex, float threshold)
			{
				GamepadIndex = gamepadIndex;
				Threshold = threshold;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].RightTriggerCheck(Threshold); }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].RightTriggerPressed(Threshold); }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].RightTriggerReleased(Threshold); }
			}
		}

		#endregion

		#region Pad DPad

		public class PadDPadRight : Node
		{
			public int GamepadIndex;

			public PadDPadRight(int gamepadIndex)
			{
				GamepadIndex = gamepadIndex;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].DPadRightCheck; }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].DPadRightPressed; }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].DPadRightReleased; }
			}
		}

		public class PadDPadLeft : Node
		{
			public int GamepadIndex;

			public PadDPadLeft(int gamepadIndex)
			{
				GamepadIndex = gamepadIndex;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].DPadLeftCheck; }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].DPadLeftPressed; }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].DPadLeftReleased; }
			}
		}

		public class PadDPadUp : Node
		{
			public int GamepadIndex;

			public PadDPadUp(int gamepadIndex)
			{
				GamepadIndex = gamepadIndex;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].DPadUpCheck; }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].DPadUpPressed; }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].DPadUpReleased; }
			}
		}

		public class PadDPadDown : Node
		{
			public int GamepadIndex;

			public PadDPadDown(int gamepadIndex)
			{
				GamepadIndex = gamepadIndex;
			}

			public override bool Check
			{
				get { return MInput.GamePads[GamepadIndex].DPadDownCheck; }
			}

			public override bool Pressed
			{
				get { return MInput.GamePads[GamepadIndex].DPadDownPressed; }
			}

			public override bool Released
			{
				get { return MInput.GamePads[GamepadIndex].DPadDownReleased; }
			}
		}

		#endregion

		#region Mouse

		public class MouseLeftButton : Node
		{
			public override bool Check
			{
				get { return MInput.Mouse.CheckLeftButton; }
			}

			public override bool Pressed
			{
				get { return MInput.Mouse.PressedLeftButton; }
			}

			public override bool Released
			{
				get { return MInput.Mouse.ReleasedLeftButton; }
			}
		}

		public class MouseRightButton : Node
		{
			public override bool Check
			{
				get { return MInput.Mouse.CheckRightButton; }
			}

			public override bool Pressed
			{
				get { return MInput.Mouse.PressedRightButton; }
			}

			public override bool Released
			{
				get { return MInput.Mouse.ReleasedRightButton; }
			}
		}

		public class MouseMiddleButton : Node
		{
			public override bool Check
			{
				get { return MInput.Mouse.CheckMiddleButton; }
			}

			public override bool Pressed
			{
				get { return MInput.Mouse.PressedMiddleButton; }
			}

			public override bool Released
			{
				get { return MInput.Mouse.ReleasedMiddleButton; }
			}
		}

		#endregion

		#region Other Virtual Inputs

		public class VirtualAxisTrigger : Node
		{
			public enum Modes { LargerThan, LessThan, Equals };

			public VirtualInput.ThresholdModes Mode;
			public float Threshold;

			private VirtualAxis axis;

			public VirtualAxisTrigger(VirtualAxis axis, VirtualInput.ThresholdModes mode, float threshold)
			{
				this.axis = axis;
				Mode = mode;
				Threshold = threshold;
			}

			public override bool Check
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value <= Threshold;
					else
						return axis.Value == Threshold;
				}
			}

			public override bool Pressed
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value >= Threshold && axis.PreviousValue < Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value <= Threshold && axis.PreviousValue > Threshold;
					else
						return axis.Value == Threshold && axis.PreviousValue != Threshold;
				}
			}

			public override bool Released
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value < Threshold && axis.PreviousValue >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value > Threshold && axis.PreviousValue <= Threshold;
					else
						return axis.Value != Threshold && axis.PreviousValue == Threshold;
				}
			}
		}

		public class VirtualIntegerAxisTrigger : Node
		{
			public enum Modes { LargerThan, LessThan, Equals };

			public VirtualInput.ThresholdModes Mode;
			public int Threshold;

			private VirtualIntegerAxis axis;

			public VirtualIntegerAxisTrigger(VirtualIntegerAxis axis, VirtualInput.ThresholdModes mode, int threshold)
			{
				this.axis = axis;
				Mode = mode;
				Threshold = threshold;
			}

			public override bool Check
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value <= Threshold;
					else
						return axis.Value == Threshold;
				}
			}

			public override bool Pressed
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value >= Threshold && axis.PreviousValue < Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value <= Threshold && axis.PreviousValue > Threshold;
					else
						return axis.Value == Threshold && axis.PreviousValue != Threshold;
				}
			}

			public override bool Released
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return axis.Value < Threshold && axis.PreviousValue >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return axis.Value > Threshold && axis.PreviousValue <= Threshold;
					else
						return axis.Value != Threshold && axis.PreviousValue == Threshold;
				}
			}
		}

		public class VirtualJoystickXTrigger : Node
		{
			public enum Modes { LargerThan, LessThan, Equals };

			public VirtualInput.ThresholdModes Mode;
			public float Threshold;

			private VirtualJoystick joystick;

			public VirtualJoystickXTrigger(VirtualJoystick joystick, VirtualInput.ThresholdModes mode, float threshold)
			{
				this.joystick = joystick;
				Mode = mode;
				Threshold = threshold;
			}

			public override bool Check
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X <= Threshold;
					else
						return joystick.Value.X == Threshold;
				}
			}

			public override bool Pressed
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X >= Threshold && joystick.PreviousValue.X < Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X <= Threshold && joystick.PreviousValue.X > Threshold;
					else
						return joystick.Value.X == Threshold && joystick.PreviousValue.X != Threshold;
				}
			}

			public override bool Released
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X < Threshold && joystick.PreviousValue.X >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X > Threshold && joystick.PreviousValue.X <= Threshold;
					else
						return joystick.Value.X != Threshold && joystick.PreviousValue.X == Threshold;
				}
			}
		}

		public class VirtualJoystickYTrigger : Node
		{
			public VirtualInput.ThresholdModes Mode;
			public float Threshold;

			private VirtualJoystick joystick;

			public VirtualJoystickYTrigger(VirtualJoystick joystick, VirtualInput.ThresholdModes mode, float threshold)
			{
				this.joystick = joystick;
				Mode = mode;
				Threshold = threshold;
			}

			public override bool Check
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X <= Threshold;
					else
						return joystick.Value.X == Threshold;
				}
			}

			public override bool Pressed
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X >= Threshold && joystick.PreviousValue.X < Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X <= Threshold && joystick.PreviousValue.X > Threshold;
					else
						return joystick.Value.X == Threshold && joystick.PreviousValue.X != Threshold;
				}
			}

			public override bool Released
			{
				get
				{
					if (Mode == VirtualInput.ThresholdModes.LargerThan)
						return joystick.Value.X < Threshold && joystick.PreviousValue.X >= Threshold;
					else if (Mode == VirtualInput.ThresholdModes.LessThan)
						return joystick.Value.X > Threshold && joystick.PreviousValue.X <= Threshold;
					else
						return joystick.Value.X != Threshold && joystick.PreviousValue.X == Threshold;
				}
			}
		}

		public class VButton : Node
		{
			public VirtualButton button;

			public VButton(VirtualButton _button)
			{
				button = _button;
			}

			public override bool Check
			{
				get { return button; }
			}

			public override bool Pressed
			{
				get { return button.Pressed; }
			}

			public override bool Released
			{
				get { return button.Released ; }
			}
		}

		public class DelegateButton : Node {

			public Func<bool> func;
			bool check, prev;

			public DelegateButton(Func<bool> func) {
				this.func = func;
			}

			public override bool Check => check;

			public override bool Pressed => check && !prev;

			public override bool Released => !check && prev;

			public override void Update() {
				base.Update();
				prev = check;
				check = func();
			}
		}

		#endregion
	}
}
