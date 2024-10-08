﻿using System.Collections;
using System.Collections.Generic;

namespace Monocle
{
    public class CoroutineHolder : Component
    {
        private List<CoroutineData> coroutineList;
        private HashSet<CoroutineData> toRemove;
        private int nextID;
        private bool isRunning;

        public CoroutineHolder()
            : base(true, false)
        {
            coroutineList = new List<CoroutineData>();
            toRemove = new HashSet<CoroutineData>();
        }

        public override void Update()
        {
            isRunning = true;
            for (int i = 0; i < coroutineList.Count; i++)
            {
                var now = coroutineList[i].Data.Peek();

                if (now.Finished) {
                    coroutineList[i].Data.Pop();
					if (coroutineList[i].Data.Count == 0)
						toRemove.Add(coroutineList[i]);
				}
                else {
                    now.Update();
                }
            }
            isRunning = false;

            if (toRemove.Count > 0)
            {
                foreach (var r in toRemove)
                    coroutineList.Remove(r);
                toRemove.Clear();
            }
        }

        public void EndCoroutine(int id)
        {
            foreach (var c in coroutineList)
            {
                if (c.ID == id)
                {
                    if (isRunning)
                        toRemove.Add(c);
                    else
                        coroutineList.Remove(c);
                    break;
                }
            }
        }

        public int StartCoroutine(Coroutine functionCall)
        {
            var data = new CoroutineData(nextID++, functionCall);
            coroutineList.Add(data);
            return data.ID;
        }

        public static IEnumerator WaitForFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return 0;
        }

        private class CoroutineData
        {
            public int ID;
            public Stack<Coroutine> Data;

            public CoroutineData(int id, Coroutine functionCall)
            {
                ID = id;
                Data = new Stack<Coroutine>();
                Data.Push(functionCall);
            }
        }
    }
}
