using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Monocle
{
    public struct EntityID {
        public string UUID { get; private set; }
        public bool FromData { get; internal set; }
        public EntityID(EntityData data) {
            UUID = data.id;
            FromData = data.constantEntity;
        }
    }
    public interface IMonocleRenderer {
        public uint Tag { get; }
        public int RenderOrder { get; }

        public void Render();
    }
    public class Entity : IEnumerable<Component>, IEnumerable, IMonocleRenderer {
        public const int MAX_GAME_UIDEPTH = 2048;

        internal bool wasLoaded;

        public bool Active = true;
        public bool Visible = true;
        public Vector3 Position;

        public Scene Scene { get; private set; }
        public ComponentList Components { get; private set; }

        public bool RunWhileFrozen { get; set; }

        public int RenderOrder { get; set; }
        /// <summary>
        /// A number to determine in which order entities are updated.  Bigger numbers happen later
        /// </summary>
        public int UpdateOrder { get; protected set; }

        private uint tag;
        //private Collider collider;

        public EntityID ID { get; private set; }

        public Entity(EntityData data, Vector3 offset) {

            Position = data.Position + offset;

            Components = new ComponentList(this);

            ID = new EntityID(data);

            wasLoaded = true;
        }

        public Entity(EntityData data)
            : this(data, Vector3.Zero) {

        }

        public Entity() :
            this(EntityData.Default, Vector3.Zero) {
        }

        /// <summary>
        /// Called when the containing Scene Begins
        /// </summary>
        public virtual void SceneBegin(Scene scene)
        {

        }

        /// <summary>
        /// Called when the containing Scene Ends
        /// </summary>
        public virtual void SceneEnd(Scene scene)
        {
            if (Components != null)
                foreach (var c in Components)
                    c.SceneEnd(scene);
        }

        /// <summary>
        /// Called before the frame starts, after Entities are added and removed, on the frame that the Entity was added
        /// Useful if you added two Entities in the same frame, and need them to detect each other before they start Updating
        /// </summary>
        /// <param name="scene"></param>
        public virtual void Awake(Scene scene)
        {
            if (Components != null)
                foreach (var c in Components)
                    c.EntityAwake();
        }

        /// <summary>
        /// Called when this Entity is added to a Scene, which only occurs immediately before each Update. 
        /// Keep in mind, other Entities to be added this frame may be added after this Entity. 
        /// See Awake() for after all Entities are added, but still before the frame Updates.
        /// </summary>
        /// <param name="scene"></param>
        public virtual void Added(Scene scene)
        {
            Scene = scene;
            if (Components != null)
                foreach (var c in Components)
                    c.EntityAdded(scene);
            //Scene.SetActualDepth(this);
        }

        /// <summary>
        /// Called when the Entity is removed from a Scene
        /// </summary>
        /// <param name="scene"></param>
        public virtual void Removed(Scene scene)
        {
            if (Components != null)
                foreach (var c in Components)
                    c.EntityRemoved(scene);
            if (scene == Scene)
                Scene = null;
        }

        public virtual void OnUnloaded() {

		}
        public virtual void OnLoaded() {

		}

        /// <summary>
        /// Do game logic here, but do not render here. Not called if the Entity is not Active
        /// </summary>
        public virtual void Update()
        {
            Components.Update();
        }

        /// <summary>
        /// Draw the Entity here. Not called if the Entity is not Visible
        /// </summary>
        public virtual void Render() { }


		/// <summary>
		/// Called when the graphics device resets. When this happens, any RenderTargets or other contents of VRAM will be wiped and need to be regenerated
		/// </summary>
		public virtual void HandleGraphicsReset()
        {
            Components.HandleGraphicsReset();
        }

        public virtual void OnSave() { }

        public virtual void OnReset() { }

        public virtual void HandleGraphicsCreate()
        {
            Components.HandleGraphicsCreate();
        }

        public void RemoveSelf()
        {
            if (Scene != null)
                Scene.Entities.Remove(this);
        }

        public float X
        {
            get { return Position.X; }
            set { Position.X = value; }
        }

        public float Y
        {
            get { return Position.Y; }
            set { Position.Y = value; }
        }

        public float Z {
            get { return Position.Z; }
            set { Position.Z = value; }
        }

        #region Tag

        public uint Tag
        {
            get
            {
                return tag;
            }

            set
            {
                if (tag != value)
                {
                    if (Scene != null)
                    {
                        for (int i = 0; i < Monocle.BitTag.TotalTags; i++)
                        {
                            int check = 1 << i;
                            bool add = (value & check) != 0;
                            bool has = (Tag & check) != 0;

                            if (has != add)
                            {
                                if (add)
                                    Scene.TagLists[i].Add(this);
                                else
                                    Scene.TagLists[i].Remove(this);
                            }
                        }
                    }

                    tag = value;
                }
            }
        }

        public bool TagFullCheck(uint tag)
        {
            return (this.tag & tag) == tag;
        }

        public bool TagCheck(uint tag)
        {
            return (this.tag & tag) != 0;
        }

        public void AddTag(uint tag)
        {
            Tag |= tag;
        }

        public void RemoveTag(uint tag)
        {
            Tag &= ~tag;
        }

        #endregion

        #region Collision Shortcuts

//        #region Collide Check

//        public bool CollideCheck(Entity other)
//        {
//            return Collide.Check(this, other);
//        }

//        public bool CollideCheck(Entity other, Vector3 at)
//        {
//            return Collide.Check(this, other, at);
//        }

//        public bool CollideCheck(BitTag tag)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.Check(this, Scene[tag]);
//        }

//        public bool CollideCheck(BitTag tag, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.Check(this, Scene[tag], at);
//        }

//        public bool CollideCheck<T>() where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            return Collide.Check(this, Scene.Tracker.Entities[typeof(T)]);
//        }

//        public bool CollideCheck<T>(Vector3 at) where T : Entity
//        {
//            return Collide.Check(this, Scene.Tracker.Entities[typeof(T)], at);
//        }

//        public bool CollideCheck<T, Exclude>() where T : Entity where Exclude : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked objects when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(Exclude)))
//                throw new Exception("Excluded type is an untracked Entity type!");
//#endif

//            var exclude = Scene.Tracker.Entities[typeof(Exclude)];
//            foreach (var e in Scene.Tracker.Entities[typeof(T)])
//                if (!exclude.Contains(e))
//                    if (Collide.Check(this, e))
//                        return true;
//            return false;
//        }

//        public bool CollideCheck<T, Exclude>(Vector3 at) where T : Entity where Exclude : Entity
//        {
//            var was = Position;
//            Position = at;
//            var ret = CollideCheck<T, Exclude>();
//            Position = was;
//            return ret;
//        }


//        #endregion

//        #region Collide CheckOutside

//        public bool CollideCheckOutside(Entity other, Vector3 at)
//        {
//            return !Collide.Check(this, other) && Collide.Check(this, other, at);
//        }

//        public bool CollideCheckOutside(BitTag tag, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif

//            foreach (var entity in Scene[tag])
//                if (!Collide.Check(this, entity) && Collide.Check(this, entity, at))
//                    return true;

//            return false;
//        }

//        public bool CollideCheckOutside<T>(Vector3 at) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            foreach (var entity in Scene.Tracker.Entities[typeof(T)])
//                if (!Collide.Check(this, entity) && Collide.Check(this, entity, at))
//                    return true;
//            return false;
//        }

//        #endregion

//        #region Collide First

//        public Entity CollideFirst(BitTag tag)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.First(this, Scene[tag]);
//        }

//        public Entity CollideFirst(BitTag tag, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.First(this, Scene[tag], at);
//        }

//        public T CollideFirst<T>() where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif
//            return Collide.First(this, Scene.Tracker.Entities[typeof(T)]) as T;
//        }

//        public T CollideFirst<T>(Vector3 at) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                 throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif
//            return Collide.First(this, Scene.Tracker.Entities[typeof(T)], at) as T;
//        }

//        #endregion

//        #region Collide FirstOutside

//        public Entity CollideFirstOutside(BitTag tag, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif

//            foreach (var entity in Scene[tag])
//                if (!Collide.Check(this, entity) && Collide.Check(this, entity, at))
//                    return entity;
//            return null;
//        }

//        public T CollideFirstOutside<T>(Vector3 at) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            foreach (var entity in Scene.Tracker.Entities[typeof(T)])
//                if (!Collide.Check(this, entity) && Collide.Check(this, entity, at))
//                    return entity as T;
//            return null;
//        }

//        #endregion

//        #region Collide All

//        public List<Entity> CollideAll(BitTag tag)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.All(this, Scene[tag]);
//        }

//        public List<Entity> CollideAll(BitTag tag, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif
//            return Collide.All(this, Scene[tag], at);
//        }

//        public List<Entity> CollideAll<T>() where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            return Collide.All(this, Scene.Tracker.Entities[typeof(T)]);
//        }

//        public List<Entity> CollideAll<T>(Vector3 at) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            return Collide.All(this, Scene.Tracker.Entities[typeof(T)], at);
//        }

//        public List<Entity> CollideAll<T>(Vector3 at, List<Entity> into) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            into.Clear();
//            return Collide.All(this, Scene.Tracker.Entities[typeof(T)], into, at);
//        }


//        #endregion

//        #region Collide Do

//        public bool CollideDo(BitTag tag, Action<Entity> action)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif

//            bool hit = false;
//            foreach (var other in Scene[tag])
//            {
//                if (CollideCheck(other))
//                {
//                    action(other);
//                    hit = true;
//                }
//            }
//            return hit;
//        }

//        public bool CollideDo(BitTag tag, Action<Entity> action, Vector3 at)
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against a tag list when it is not a member of a Scene");
//#endif

//            bool hit = false;
//            var was = Position;
//            Position = at;

//            foreach (var other in Scene[tag])
//            {
//                if (CollideCheck(other))
//                {
//                    action(other);
//                    hit = true;
//                }
//            }

//            Position = was;
//            return hit;
//        }

//        public bool CollideDo<T>(Action<T> action) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            bool hit = false;
//            foreach (var other in Scene.Tracker.Entities[typeof(T)])
//            {
//                if (CollideCheck(other))
//                {
//                    action(other as T);
//                    hit = true;
//                }
//            }
//            return hit;
//        }

//        public bool CollideDo<T>(Action<T> action, Vector3 at) where T : Entity
//        {
//#if DEBUG
//            if (Scene == null)
//                throw new Exception("Can't collide check an Entity against tracked Entities when it is not a member of a Scene");
//            else if (!Scene.Tracker.Entities.ContainsKey(typeof(T)))
//                throw new Exception("Can't collide check an Entity against an untracked Entity type");
//#endif

//            bool hit = false;
//            var was = Position;
//            Position = at;

//            foreach (var other in Scene.Tracker.Entities[typeof(T)])
//            {
//                if (CollideCheck(other))
//                {
//                    action(other as T);
//                    hit = true;
//                }
//            }

//            Position = was;
//            return hit;
//        }

//        #endregion

//        #region Collide Geometry

//        public bool CollidePoint(Vector3 point)
//        {
//            return Collide.CheckPoint(this, point);
//        }

//        public bool CollidePoint(Vector3 point, Vector3 at)
//        {
//            return Collide.CheckPoint(this, point, at);
//        }

//        public bool CollideLine(Vector3 from, Vector3 to)
//        {
//            return Collide.CheckLine(this, from, to);
//        }

//        public bool CollideLine(Vector3 from, Vector3 to, Vector3 at)
//        {
//            return Collide.CheckLine(this, from, to, at);
//        }

//        public bool CollideRect(Hitbox3D rect)
//        {
//            return Collide.CheckRect(this, rect);
//        }

//        public bool CollideRect(Hitbox3D rect, Vector3 at)
//        {
//            return Collide.CheckRect(this, rect, at);
//        }

//        #endregion

        #endregion

        #region Components Shortcuts

        /// <summary>
        /// Shortcut function for adding a Component to the Entity's Components list
        /// </summary>
        /// <param name="component">The Component to add</param>
        public void Add(Component component)
        {
            Components.Add(component);
        }

        /// <summary>
        /// Shortcut function for removing an Component from the Entity's Components list
        /// </summary>
        /// <param name="component">The Component to remove</param>
        public void Remove(Component component)
        {
            Components.Remove(component);
        }

        /// <summary>
        /// Shortcut function for adding a set of Components from the Entity's Components list
        /// </summary>
        /// <param name="components">The Components to add</param>
        public void Add(params Component[] components)
        {
            Components.Add(components);
        }

        /// <summary>
        /// Shortcut function for removing a set of Components from the Entity's Components list
        /// </summary>
        /// <param name="components">The Components to remove</param>
        public void Remove(params Component[] components)
        {
            Components.Remove(components);
        }

        public T Get<T>() where T : Component
        {
            return Components.Get<T>();
        }

        /// <summary>
        /// Allows you to iterate through all Components in the Entity
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Component> GetEnumerator()
        {
            return Components.GetEnumerator();
        }

        /// <summary>
        /// Allows you to iterate through all Components in the Entity
        /// </summary>
        /// <returns></returns>
        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Misc Utils

        public Entity Closest(params Entity[] entities)
        {
            Entity closest = entities[0];
            float dist = Vector3.DistanceSquared(Position, closest.Position);

            for (int i = 1; i < entities.Length; i++)
            {
                float current = Vector3.DistanceSquared(Position, entities[i].Position);
                if (current < dist)
                {
                    closest = entities[i];
                    dist = current;
                }
            }

            return closest;
        }

        public Entity Closest(BitTag tag)
        {
            var list = Scene[tag];
            Entity closest = null;
            float dist;

            if (list.Count >= 1)
            {
                closest = list[0];
                dist = Vector3.DistanceSquared(Position, closest.Position);

                for (int i = 1; i < list.Count; i++)
                {
                    float current = Vector3.DistanceSquared(Position, list[i].Position);
                    if (current < dist)
                    {
                        closest = list[i];
                        dist = current;
                    }
                }
            }

            return closest;
        }

        public T SceneAs<T>() where T : Scene
        {
            return Scene as T;
        }

        #endregion
    }
}
