using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Tokens;

namespace Monocle
{
    public class EntityList : IEnumerable<Entity>, IEnumerable
    {
        public Scene Scene { get; private set; }

        private List<Entity> entities;
        private List<Entity> toAdd;
        private List<Entity> toAwake;
        private List<Entity> toRemove;

        private HashSet<Entity> current;
        private HashSet<Entity> adding;
        private HashSet<Entity> removing;

        private bool unsorted;

        internal EntityList(Scene scene)
        {
            Scene = scene;

            entities = new List<Entity>();
            toAdd = new List<Entity>();
            toAwake = new List<Entity>();
            toRemove = new List<Entity>();

            current = new HashSet<Entity>();
            adding = new HashSet<Entity>();
            removing = new HashSet<Entity>();
        }

        internal void MarkUnsorted()
        {
            unsorted = true;
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
                        entities.Add(entity);
                        entity.OnSave();

                        if (Scene != null)
                        {
                            Scene.TagLists.EntityAdded(entity);
                            Scene.Tracker.EntityAdded(entity);
                            
                        }
                    }
                }

                unsorted = true;
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    var entity = toRemove[i];
                    if (entities.Contains(entity))
                    {
                        current.Remove(entity);
                        entities.Remove(entity);

                        if (Scene != null)
                        {
                            entity.Removed(Scene);
                            Scene.TagLists.EntityRemoved(entity);
                            Scene.Tracker.EntityRemoved(entity);
                            Engine.Pooler.EntityRemoved(entity);
                        }
                    }
                }

                toRemove.Clear();
                removing.Clear();
            }

            if (unsorted)
            {
                unsorted = false;
                entities.Sort(CompareDepth);
            }

            if (toAdd.Count > 0)
            {
                toAwake.AddRange(toAdd);
                toAdd.Clear();
                adding.Clear();

            }
        }

        public void Add(Entity entity)
        {
            if (entity == null)
                return;

            if (!adding.Contains(entity) && !current.Contains(entity)) {

                if (Scene != null)
                    entity.Added(Scene);

                adding.Add(entity);
                toAdd.Add(entity);
            }
        }

        public void Remove(Entity entity)
        {
            if (!removing.Contains(entity) && current.Contains(entity))
            {
                removing.Add(entity);
                toRemove.Add(entity);
            }
        }

        public void Add(IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
                Add(entity);
        }

        public void Remove(IEnumerable<Entity> entities)
        {
            foreach (var entity in entities)
                Remove(entity);
        }

        public void Add(params Entity[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
                Add(entities[i]);
        }

        public void Remove(params Entity[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
                Remove(entities[i]);
        }

        public int Count
        {
            get
            {
                return entities.Count;
            }
        }

        public Entity this[int index]
        {
            get
            {
                if (index < 0 || index >= entities.Count)
                    throw new IndexOutOfRangeException();
                else
                    return entities[index];
            }
        }

        public int AmountOf<T>() where T : Entity
        {
            int count = 0;
            foreach (var e in entities)
                if (e is T)
                    count++;

            return count;
        }

        public T FindFirst<T>() where T : Entity
        {
            foreach (var e in entities)
                if (e is T)
                    return e as T;

            return null;
        }

        public T FindByID<T>(string id) where T : Entity {

            foreach (var e in entities)
                if (e.ID.UUID == id)
                    return e as T;

            return null;
        }

        public Entity FindByID(string id) {

            foreach (var e in entities)
                if (e.ID.UUID == id)
                    return e;

            return null;
        }

        public List<T> FindAll<T>() where T : Entity
        {
            List<T> list = new List<T>();

            foreach (var e in entities)
                if (e is T)
                    list.Add(e as T);

            return list;
        }
        public List<T> IFindAll<T>() {

            List<T> list = new List<T>();

            foreach (var e in entities)
                if (e is T) {
                    dynamic item = e;
                    list.Add(item);
                }

            return list;

        }

        public IEnumerable<Entity> GetAll() {

            foreach (var e in entities)
                yield return e;
        }

        public IEnumerable<Entity> GetAll(uint tag) {

            foreach (var e in entities) {
                if (e.TagCheck(tag))
                    yield return e;
            }
        }

        public void With<T>(Action<T> action) where T : Entity
        {
            foreach (var e in entities)
                if (e is T)
                    action(e as T);
        }

        public IEnumerator<Entity> GetEnumerator()
        {
            return entities.GetEnumerator();
        }

        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Entity[] ToArray()
        {
            return entities.ToArray<Entity>();
        }

        public bool HasVisibleEntities(uint matchTags)
        {
            foreach (var entity in entities)
                if (entity.Visible && entity.TagCheck(matchTags))
                    return true;
            return false;
        }

		internal void CheckAwake() {
			if (Engine.FreezeTimer > 0)
				return;

			foreach (var entity in toAwake)
				if (entity.Scene == Scene)
					entity.Awake(Scene);
			toAwake.Clear();
		}
		internal void Update() {
			if (Engine.FreezeTimer > 0)
				return;

			foreach (var entity in toAwake)
				if (entity.Scene == Scene)
					entity.Awake(Scene);
			toAwake.Clear();

			foreach (var entity in entities)
				if (entity.Active)
					entity.Update();
		}

		public void Render()
        {
            foreach (var entity in entities) {
                if (entity.Visible)
                    entity.Render();
                foreach (var comp in entity.Components) {
                    if (comp.Visible)
                        comp.Render();
                }
            }
        }

        public void RenderOnly(uint matchTags)
        {
            foreach (var entity in entities) {

				if (entity.Visible && entity.TagCheck(matchTags))
					entity.Render();
				foreach (var comp in entity.Components) {
					if (comp.Visible && comp.TagCheck(matchTags))
						comp.Render();
				}
			}
        }

        public void RenderOnlyFullMatch(uint matchTags) {
			foreach (var entity in entities) {

				if (entity.Visible && entity.TagFullCheck(matchTags))
					entity.Render();
				foreach (var comp in entity.Components) {
					if (comp.Visible && comp.TagFullCheck(matchTags))
						comp.Render();
				}
			}
        }

        public void RenderExcept(uint excludeTags) {
			foreach (var entity in entities) {

				if (entity.Visible && !entity.TagCheck(excludeTags))
					entity.Render();
				foreach (var comp in entity.Components) {
					if (comp.Visible && !comp.TagCheck(excludeTags))
						comp.Render();
				}
			}
        }


        internal void HandleGraphicsReset()
        {
            foreach (var entity in entities)
                entity.HandleGraphicsReset();
        }

        internal void HandleGraphicsCreate()
        {
            foreach (var entity in entities)
                entity.HandleGraphicsCreate();
        }

        public static Comparison<Entity> CompareDepth = (a, b) => { return a.UpdateOrder - b.UpdateOrder; };
    }
}
