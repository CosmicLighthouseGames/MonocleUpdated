using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Monocle
{
    public class CoroutineList : IEnumerable<Coroutine>, IEnumerable
    {

        private List<Coroutine> coroutines;
        private List<Coroutine> toAdd;
        private List<Coroutine> toAwake;
        private List<Coroutine> toRemove;

        private HashSet<Coroutine> current;
        private HashSet<Coroutine> adding;
        private HashSet<Coroutine> removing;

        internal CoroutineList()
        {

            coroutines = new List<Coroutine>();
            toAdd = new List<Coroutine>();
            toAwake = new List<Coroutine>();
            toRemove = new List<Coroutine>();

            current = new HashSet<Coroutine>();
            adding = new HashSet<Coroutine>();
            removing = new HashSet<Coroutine>();
        }

        public void UpdateLists()
        {
            if (toAdd.Count > 0)
            {
                for (int i = 0; i < toAdd.Count; i++)
                {
                    var entity = toAdd[i];
                    if (!current.Contains(entity))
                    {
                        current.Add(entity);
                        coroutines.Add(entity);
                    }
                }

            }

            foreach (var routine in coroutines) {
                if (!routine.Active)
                    toRemove.Add(routine);
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    var entity = toRemove[i];
                    if (coroutines.Contains(entity))
                    {
                        current.Remove(entity);
                        coroutines.Remove(entity);

                    }
                }

                toRemove.Clear();
                removing.Clear();
            }

            if (toAdd.Count > 0)
            {
                toAwake.AddRange(toAdd);
                toAdd.Clear();
                adding.Clear();

                toAwake.Clear();
            }
        }

        public void Add(Coroutine entity)
        {
            if (!adding.Contains(entity) && !current.Contains(entity)) {


                adding.Add(entity);
                toAdd.Add(entity);
            }
        }

        public void Remove(Coroutine entity)
        {
            if (!removing.Contains(entity) && current.Contains(entity))
            {
                removing.Add(entity);
                toRemove.Add(entity);
            }
        }

        public void Add(IEnumerable<Coroutine> entities)
        {
            foreach (var entity in entities)
                Add(entity);
        }

        public void Remove(IEnumerable<Coroutine> entities)
        {
            foreach (var entity in entities)
                Remove(entity);
        }

        public void Add(params Coroutine[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
                Add(entities[i]);
        }

        public void Remove(params Coroutine[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
                Remove(entities[i]);
        }

        public int Count
        {
            get
            {
                return coroutines.Count;
            }
        }

        public Coroutine this[int index]
        {
            get
            {
                if (index < 0 || index >= coroutines.Count)
                    throw new IndexOutOfRangeException();
                else
                    return coroutines[index];
            }
        }

        public IEnumerable<Coroutine> GetAll() {

            foreach (var e in coroutines)
                yield return e;
        }

        public IEnumerator<Coroutine> GetEnumerator()
        {
            return coroutines.GetEnumerator();
        }

        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Coroutine[] ToArray()
        {
            return coroutines.ToArray<Coroutine>();
        }

        internal void Update()
        {
            if (Engine.FreezeTimer > 0)
                return;

            foreach (var routine in coroutines)
                if (routine.Active)
                    routine.Update();
        }

        public void Render()
        {
            foreach (var entity in coroutines)
                if (entity.Visible)
                    entity.Render();
        }

        internal void HandleGraphicsReset()
        {
            foreach (var entity in coroutines)
                entity.HandleGraphicsReset();
        }

        internal void HandleGraphicsCreate()
        {
            foreach (var entity in coroutines)
                entity.HandleGraphicsCreate();
        }
    }
}
