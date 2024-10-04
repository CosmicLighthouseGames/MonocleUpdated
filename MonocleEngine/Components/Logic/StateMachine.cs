using System;
using System.Collections;
using System.Collections.Generic;

namespace Monocle
{
    public class StateMachine : Component
    {
        private int state;
        private List<string> stateNames;
        private List<Action> begins;
        private List<Func<int>> updates;
        private List<Action> ends;
        private List<Func<IEnumerator>> coroutines;
        private Coroutine currentCoroutine;

        public bool ChangedStates;
        public bool Log;
        public int PreviousState { get; private set; }
        public bool Locked;
        public bool RoutineRunning => currentCoroutine != null && !currentCoroutine.Finished;

        public float StateTimeActive { get; private set; }


		public StateMachine(int maxStates = 10)
            : base(true, false)
        {
            PreviousState = state = -1;

            begins = new List<Action>(new Action[maxStates]);
            updates = new List<Func<int>>(new Func<int>[maxStates]);
            ends = new List<Action>(new Action[maxStates]);
            coroutines = new List<Func<IEnumerator>>(new Func<IEnumerator>[maxStates]);
            stateNames = new List<string>(new string[maxStates]);

            currentCoroutine = new Coroutine();
            currentCoroutine.RemoveOnComplete = false;
        }

        public override void Added(Entity entity)
        {
            base.Added(entity);

            if (state == -1)
                State = 0;
        }

        public int State
        {
            get { return state; }
            set
            {
#if DEBUG
                if (value >= updates.Count || value < 0)
                    throw new Exception("StateMachine state out of range");
#endif

                if (!Locked && state != value)
                {
					if (Log)
                        Calc.Log("Enter State " + value + " (leaving " + state + ")");

                    ChangedStates = true;
                    PreviousState = state;
                    state = value;

                    if (PreviousState != -1 && ends[PreviousState] != null)
                    {
                        if (Log)
                            Calc.Log("Calling End " + PreviousState);
                        ends[PreviousState]();
					}

					StateTimeActive = 0;

					if (begins[state] != null)
                    {
                        if (Log)
                            Calc.Log("Calling Begin " + state);
                        begins[state]();
                    }

                    if (coroutines[state] != null)
                    {
                        if (Log)
                            Calc.Log("Starting Coroutine " + state);
                        currentCoroutine.Replace(coroutines[state]());
                    }
                    else
                        currentCoroutine.Cancel();
                }
            }
        }
        public string StateName
        {
            get { return stateNames[state]; }
            set
            {
                if (stateNames.Contains(value))
                    State = stateNames.IndexOf(value);
            }
        }

        public void SetCallbacks(int state, Func<int> onUpdate, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null, string name = null)
        {
            while (updates.Count <= state)
            {
                CreateNewCallback(null);
            }
            
            updates[state] = onUpdate;
            begins[state] = begin;
            ends[state] = end;
            coroutines[state] = coroutine;
            if (name != null)
                SetStateName(state, name);
        }
        public void SetCallbacks(string state, Func<int> onUpdate, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null)
        {
            if (!stateNames.Contains(state))
            {
				if (!int.TryParse(state, out int stateIndex))
					throw new ArgumentException();

				while (updates.Count <= stateIndex)
                {
                    CreateNewCallback(null);
                }
            }

            SetCallbacks(GetStateNameIndex(state), onUpdate, coroutine, begin, end);
        }
        public int CreateNewCallback(Func<int> onUpdate, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null, string name = null)
        {
            int retVal = updates.Count;
            
            if (name == null)
            {
                name = retVal.ToString();
            }
            if (stateNames.Contains(name))
            {
                throw new SystemException("State of this ID already exists");
            }

            updates.Add(onUpdate);
            begins.Add(begin);
            ends.Add(end);
            coroutines.Add(coroutine);
            stateNames.Add(name);

            return retVal;
        }

        public void ReflectState(Entity from, int index, string name)
        {
            updates[index] = (Func<int>)Calc.GetMethod<Func<int>>(from, name + "Update");
            begins[index] = (Action)Calc.GetMethod<Action>(from, name + "Begin");
            ends[index] = (Action)Calc.GetMethod<Action>(from, name + "End");
            coroutines[index] = (Func<IEnumerator>)Calc.GetMethod<Func<IEnumerator>>(from, name + "Coroutine");
        }
        public void ReflectState(Entity from, string state, string name)
        {
            ReflectState(from, GetStateNameIndex(state), name);
        }

        public int GetStateNameIndex(string state)
        {
            return stateNames.IndexOf(state);
        }
        public string GetNameOfState(int state)
        {
            return stateNames[state];
        }
        private void SetStateName(int index, string name)
        {
            if (name == null)
                return;
            if (stateNames.Contains(name))
                throw new SystemException("State of this ID already exists");

            stateNames[index] = name;
        }

        public override void Update()
        {
            ChangedStates = false;

            if (updates[state] != null)
            {
                int st = updates[state]();
                if (st >= 0)
                    State = st;
            }
            if (currentCoroutine.Active)
            {
                currentCoroutine.Update();
                if (!ChangedStates && Log && currentCoroutine.Finished)
                    Calc.Log("Finished Coroutine " + state);
            }

            StateTimeActive += Engine.DeltaTime;
        }

        public static implicit operator int(StateMachine s)
        {
            return s.state;
        }

        public void LogAllStates()
        {
            for (int i = 0; i < updates.Count; i++)
                LogState(i);
        }

        public void LogState(int index)
        {
            Calc.Log("State " + index + " (" + stateNames[index] + "): "
                + (updates[index] != null ? "U" : "")
                + (begins[index] != null ? "B" : "")
                + (ends[index] != null ? "E" : "")
                + (coroutines[index] != null ? "C" : ""));
        }
    }
}
