using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Monocle
{

    public class Scene : IEnumerable<Entity>, IEnumerable
    {
        public bool Paused;
        public float TimeActive;
        public float RawTimeActive;
        public bool Focused { get; private set; }
        public EntityList Entities { get; private set; }
        public TagLists TagLists { get; private set; }
        public RendererList RendererList { get; private set; }
        public Entity HelperEntity { get; private set; }
        public Tracker Tracker { get; private set; }

        public event Action OnEndOfFrame;

        public Scene()
        {
            Tracker = new Tracker();
            Entities = new EntityList(this);
            TagLists = new TagLists();
            RendererList = new RendererList(this);

            HelperEntity = new Entity();
            Entities.Add(HelperEntity);
        }

        public virtual void Begin()
        {
            Focused = true;
            foreach (var entity in Entities)
                entity.SceneBegin(this);
        }

        public virtual void End()
        {
            Focused = false;
            foreach (var entity in Entities)
                entity.SceneEnd(this);
        }

        public virtual void BeforeUpdate()
        {
            if (!Paused)
                TimeActive += Engine.DeltaTime;
            RawTimeActive += Engine.RawDeltaTime;

            Entities.UpdateLists();
            TagLists.UpdateLists();
            RendererList.UpdateLists();
        }

        public virtual void Update()
        {
            if (!Paused)
            {
                Entities.Update();
                RendererList.Update();
            }
        }

        public virtual void AfterUpdate()
        {
            if (OnEndOfFrame != null)
            {
                OnEndOfFrame();
                OnEndOfFrame = null;
            }
        }

        public virtual void BeforeRender()
        {
            RendererList.BeforeRender();
        }

        public virtual void Render()
        {
            RendererList.Render();
        }

        public virtual void AfterRender()
        {
            RendererList.AfterRender();
        }

        public virtual void HandleGraphicsReset()
        {
            Entities.HandleGraphicsReset();
            foreach (var render in RendererList.Renderers) {
                render.HandleGraphicsReset();
			}
            //foreach (var entity in Entities) {
            //    entity.HandleGraphicsReset();
            //}
        }

        public virtual void HandleGraphicsCreate()
        {
            Entities.HandleGraphicsCreate();
        }

        public virtual void GainFocus()
        {

        }

        public virtual void LoseFocus()
        {

        }

        #region Interval

        /// <summary>
        /// Returns whether the Scene timer has passed the given time interval since the last frame. Ex: given 2.0f, this will return true once every 2 seconds
        /// </summary>
        /// <param name="interval">The time interval to check for</param>
        /// <returns></returns>
        public bool OnInterval(float interval)
        {
            return (int)((TimeActive - Engine.DeltaTime) / interval) < (int)(TimeActive / interval);
        }

        /// <summary>
        /// Returns whether the Scene timer has passed the given time interval since the last frame. Ex: given 2.0f, this will return true once every 2 seconds
        /// </summary>
        /// <param name="interval">The time interval to check for</param>
        /// <returns></returns>
        public bool OnInterval(float interval, float offset)
        {
            return Math.Floor((TimeActive - offset - Engine.DeltaTime) / interval) < Math.Floor((TimeActive - offset) / interval);
        }

        public bool BetweenInterval(float interval)
        {
            return Calc.BetweenInterval(TimeActive, interval);
        }

        public bool OnRawInterval(float interval)
        {
            return (int)((RawTimeActive - Engine.RawDeltaTime) / interval) < (int)(RawTimeActive / interval);
        }

        public bool OnRawInterval(float interval, float offset)
        {
            return Math.Floor((RawTimeActive - offset - Engine.RawDeltaTime) / interval) < Math.Floor((RawTimeActive - offset) / interval);
        }

        public bool BetweenRawInterval(float interval)
        {
            return Calc.BetweenInterval(RawTimeActive, interval);
        }

        #endregion

        #region Collisions v Tags

        //public bool CollideCheck(Vector3 point, int tag)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollidePoint(point))
        //            return true;
        //    return false;
        //}

        //public bool CollideCheck(Vector3 from, Vector3 to, int tag)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideLine(from, to))
        //            return true;
        //    return false;
        //}

        //public bool CollideCheck(Hitbox3D rect, BitTag tag)
        //{
        //    var list = TagLists[tag.ID];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideRect(rect))
        //            return true;
        //    return false;
        //}

        //public bool CollideCheck(Hitbox3D rect, Entity entity)
        //{
        //    return (entity.Collidable && entity.CollideRect(rect));
        //}

        //public Entity CollideFirst(Vector3 point, int tag)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollidePoint(point))
        //            return list[i];
        //    return null;
        //}

        //public Entity CollideFirst(Vector3 from, Vector3 to, int tag)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideLine(from, to))
        //            return list[i];
        //    return null;
        //}

        //public Entity CollideFirst(Hitbox3D rect, int tag)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideRect(rect))
        //            return list[i];
        //    return null;
        //}

        //public void CollideInto(Vector3 point, int tag, List<Entity> hits)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollidePoint(point))
        //            hits.Add(list[i]);
        //}

        //public void CollideInto(Vector3 from, Vector3 to, int tag, List<Entity> hits)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideLine(from, to))
        //            hits.Add(list[i]);
        //}

        //public void CollideInto(Hitbox3D rect, BitTag tag, List<Entity> hits)
        //{
        //    var list = TagLists[tag.ID];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideRect(rect))
        //            hits.Add(list[i]);
        //}

        //public void CollideInto(Hitbox3D rect, List<Entity> hits) {

        //    foreach (var list in Tracker.Entities.Values) {
        //        for (int i = 0; i < list.Count; i++)
        //            if (list[i].Collidable && list[i].CollideRect(rect))
        //                hits.Add(list[i]);
        //    }
        //}

        //public List<Entity> CollideAll(Vector3 point, int tag)
        //{
        //    List<Entity> list = new List<Entity>();
        //    CollideInto(point, tag, list);
        //    return list;
        //}

        //public List<Entity> CollideAll(Vector3 from, Vector3 to, int tag)
        //{
        //    List<Entity> list = new List<Entity>();
        //    CollideInto(from, to, tag, list);
        //    return list;
        //}

        //public List<Entity> CollideAll(Hitbox3D rect, BitTag tag)
        //{
        //    List<Entity> list = new List<Entity>();
        //    CollideInto(rect, tag, list);
        //    return list;
        //}

        //public List<Entity> CollideAll(Hitbox3D rect) {
        //    List<Entity> list = new List<Entity>();
        //    CollideInto(rect, list);
        //    return list;
        //}

        //public void CollideDo(Vector3 point, int tag, Action<Entity> action)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollidePoint(point))
        //            action(list[i]);
        //}

        //public void CollideDo(Vector3 from, Vector3 to, int tag, Action<Entity> action)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideLine(from, to))
        //            action(list[i]);
        //}

        //public void CollideDo(Hitbox3D rect, int tag, Action<Entity> action)
        //{
        //    var list = TagLists[(int)tag];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Collidable && list[i].CollideRect(rect))
        //            action(list[i]);
        //}

        //public Vector3 LineWalkCheck(Vector3 from, Vector3 to, int tag, float precision)
        //{
        //    Vector3 add = to - from;
        //    add.Normalize();
        //    add *= precision;

        //    int amount = (int)Math.Floor((from - to).Length() / precision);
        //    Vector3 prev = from;
        //    Vector3 at = from + add;

        //    for (int i = 0; i <= amount; i++)
        //    {
        //        if (CollideCheck(at, tag))
        //            return prev;
        //        prev = at;
        //        at += add;
        //    }

        //    return to;
        //}

        #endregion

        #region Collisions v Tracked List Entities

  //      public bool IntersectCheck<T>(Hitbox3D rect) where T : Entity {
  //          Type rootType = null;
  //          foreach (Type t in Tracker.Entities.Keys) {
  //              if (t.IsAssignableFrom(typeof(T))) {
  //                  rootType = t;
  //                  break;
  //              }
  //          }
  //          if (rootType == null)
  //              return false;

  //          var list = Tracker.Entities[rootType];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] is T && list[i].CollideRect(rect))
  //                  return true;

  //          return false;
		//}

		//public bool CollideCheck(Vector3 point, Type t) {
		//	var list = Tracker.Entities[t];

		//	for (int i = 0; i < list.Count; i++)
		//		if (list[i].Collidable && list[i].CollidePoint(point))
		//			return true;
		//	return false;
		//}

		//public bool CollideCheck<T>(Vector3 point) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollidePoint(point))
  //                  return true;
  //          return false;
  //      }

  //      public bool CollideCheck<T>(Vector3 from, Vector3 to) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideLine(from, to))
  //                  return true;
  //          return false;
  //      }

  //      public bool CollideCheck<T>(Hitbox3D rect) where T : Entity
  //      {
  //          Type rootType = null;
  //          foreach (Type t in Tracker.Entities.Keys) {
  //              if (t.IsAssignableFrom(typeof(T))) {
  //                  rootType = t;
  //                  break;
  //              }
  //          }
  //          if (rootType == null)
  //              return false;

  //          var list = Tracker.Entities[rootType];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] is T && list[i].Collidable && list[i].CollideRect(rect))
  //                  return true;
  //          return false;
  //      }
  //      public bool CollideCheck<T>(Hitbox3D rect, Vector3 offset) where T : Entity {
  //          Vector3 position = rect.Position;
  //          rect.Position += offset;

  //          Type rootType = null;
  //          foreach (Type t in Tracker.Entities.Keys) {
  //              if (t.IsAssignableFrom(typeof(T))) {
  //                  rootType = t;
  //                  break;
  //              }
  //          }
  //          if (rootType == null)
  //              return false;

  //          var list = Tracker.Entities[rootType];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] is T && list[i].Collidable && list[i].CollideRect(rect)) {
  //                  rect.Position = position;
  //                  return true;
  //              }
  //          rect.Position = position;
  //          return false;
  //      }

  //      public bool ICollideCheck<T>(Hitbox3D rect) {

  //          var list = Tracker.Entities[typeof(Solid)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] is T && list[i].Collidable && list[i].CollideRect(rect))
  //                  return true;
  //          return false;
  //      }

  //      public T CollideFirst<T>(Vector3 point) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollidePoint(point))
  //                  return list[i] as T;
  //          return null;
  //      }

  //      public T CollideFirst<T>(Vector3 from, Vector3 to) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideLine(from, to))
  //                  return list[i] as T;
  //          return null;
  //      }

  //      public T CollideFirst<T>(Hitbox3D rect) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideRect(rect))
  //                  return list[i] as T;
  //          return null;
  //      }

  //      public void CollideInto<T>(Vector3 point, List<Entity> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollidePoint(point))
  //                  hits.Add(list[i]);
  //      }

  //      public void CollideInto<T>(Vector3 from, Vector3 to, List<Entity> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideLine(from, to))
  //                  hits.Add(list[i]);
  //      }

  //      public void CollideInto<T>(Hitbox3D rect, List<Entity> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideRect(rect))
  //                  list.Add(list[i]);
  //      }

  //      public void CollideInto<T>(Vector3 point, List<T> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollidePoint(point))
  //                  hits.Add(list[i] as T);
  //      }

  //      public void CollideInto<T>(Vector3 from, Vector3 to, List<T> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideLine(from, to))
  //                  hits.Add(list[i] as T);
  //      }

  //      public void CollideInto<T>(Hitbox3D rect, List<T> hits) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideRect(rect))
  //                  hits.Add(list[i] as T);
  //      }

  //      public List<T> CollideAll<T>(Vector3 point) where T : Entity
  //      {
  //          List<T> list = new List<T>();
  //          CollideInto<T>(point, list);
  //          return list;
  //      }

  //      public List<T> CollideAll<T>(Vector3 from, Vector3 to) where T : Entity
  //      {
  //          List<T> list = new List<T>();
  //          CollideInto<T>(from, to, list);
  //          return list;
  //      }

  //      public List<T> CollideAll<T>(Hitbox3D rect) where T : Entity
  //      {
  //          List<T> list = new List<T>();
  //          CollideInto<T>(rect, list);
  //          return list;
  //      }

  //      public IEnumerable ICollideAll<T>(Entity rect) {

  //          foreach (var ent in Entities)
  //              if (ent is T && ent.Collidable && ent.CollideCheck(rect))
  //                  yield return ent;
            
  //      }

  //      public void CollideDo<T>(Vector3 point, Action<T> action) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollidePoint(point))
  //                  action(list[i] as T);
  //      }

  //      public void CollideDo<T>(Vector3 from, Vector3 to, Action<T> action) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideLine(from, to))
  //                  action(list[i] as T);
  //      }

  //      public void CollideDo<T>(Hitbox3D rect, Action<T> action) where T : Entity
  //      {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i].Collidable && list[i].CollideRect(rect))
  //                  action(list[i] as T);
  //      }

  //      public void CollideDo<T>(Entity ent, Action<T> action) where T : Entity {
  //          var list = Tracker.Entities[typeof(T)];

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] != ent && list[i].Collidable && list[i].CollideRect(ent.Collider.Bounds))
  //                  action(list[i] as T);
  //      }

  //      public void MoveDo<T>(Entity ent, Vector3 vel, Func<T, bool> action) where T : Entity {
  //          var list = Tracker.Entities[typeof(T)];

  //          vel.X = (int)Math.Ceiling(Math.Abs(vel.X)) * Math.Sign(vel.X);
  //          vel.Y = (int)Math.Ceiling(Math.Abs(vel.Y)) * Math.Sign(vel.Y);
  //          vel.Z = (int)Math.Ceiling(Math.Abs(vel.Z)) * Math.Sign(vel.Z);

  //          Hitbox3D hitbox = ent.Collider.Clone() as Hitbox3D;
  //          hitbox.Extend(vel);
  //          hitbox.Inflate(-0.001f);

  //          for (int i = 0; i < list.Count; i++)
  //              if (list[i] != ent && list[i].Collidable && list[i].CollideRect(hitbox)) {
  //                  action(list[i] as T);
  //              }



  //      }

  //      public Vector3 LineWalkCheck<T>(Vector3 from, Vector3 to, float precision) where T : Entity
  //      {
  //          Vector3 add = to - from;
  //          add.Normalize();
  //          add *= precision;

  //          int amount = (int)Math.Floor((from - to).Length() / precision);
  //          Vector3 prev = from;
  //          Vector3 at = from + add;

  //          for (int i = 0; i <= amount; i++)
  //          {
  //              if (CollideCheck<T>(at))
  //                  return prev;
  //              prev = at;
  //              at += add;
  //          }

  //          return to;
  //      }

        #endregion

        #region Collisions v Tracked List Components

        //public bool CollideCheckByComponent<T>(Vector3 point) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollidePoint(point))
        //            return true;
        //    return false;
        //}

        //public bool CollideCheckByComponent<T>(Vector3 from, Vector3 to) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideLine(from, to))
        //            return true;
        //    return false;
        //}

        //public bool CollideCheckByComponent<T>(Hitbox3D rect) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideRect(rect))
        //            return true;
        //    return false;
        //}

        //public T CollideFirstByComponent<T>(Vector3 point) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollidePoint(point))
        //            return list[i] as T;
        //    return null;
        //}

        //public T CollideFirstByComponent<T>(Vector3 from, Vector3 to) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideLine(from, to))
        //            return list[i] as T;
        //    return null;
        //}

        //public T CollideFirstByComponent<T>(Hitbox3D rect) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideRect(rect))
        //            return list[i] as T;
        //    return null;
        //}

        //public void CollideIntoByComponent<T>(Vector3 point, List<Component> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollidePoint(point))
        //            hits.Add(list[i]);
        //}

        //public void CollideIntoByComponent<T>(Vector3 from, Vector3 to, List<Component> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideLine(from, to))
        //            hits.Add(list[i]);
        //}

        //public void CollideIntoByComponent<T>(Hitbox3D rect, List<Component> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideRect(rect))
        //            list.Add(list[i]);
        //}

        //public void CollideIntoByComponent<T>(Vector3 point, List<T> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollidePoint(point))
        //            hits.Add(list[i] as T);
        //}

        //public void CollideIntoByComponent<T>(Vector3 from, Vector3 to, List<T> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideLine(from, to))
        //            hits.Add(list[i] as T);
        //}

        //public void CollideIntoByComponent<T>(Hitbox3D rect, List<T> hits) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideRect(rect))
        //            list.Add(list[i] as T);
        //}

        //public List<T> CollideAllByComponent<T>(Vector3 point) where T : Component
        //{
        //    List<T> list = new List<T>();
        //    CollideIntoByComponent<T>(point, list);
        //    return list;
        //}

        //public List<T> CollideAllByComponent<T>(Vector3 from, Vector3 to) where T : Component
        //{
        //    List<T> list = new List<T>();
        //    CollideIntoByComponent<T>(from, to, list);
        //    return list;
        //}

        //public List<T> CollideAllByComponent<T>(Hitbox3D rect) where T : Component
        //{
        //    List<T> list = new List<T>();
        //    CollideIntoByComponent<T>(rect, list);
        //    return list;
        //}

        //public void CollideDoByComponent<T>(Vector3 point, Action<T> action) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollidePoint(point))
        //            action(list[i] as T);
        //}

        //public void CollideDoByComponent<T>(Vector3 from, Vector3 to, Action<T> action) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideLine(from, to))
        //            action(list[i] as T);
        //}

        //public void CollideDoByComponent<T>(Hitbox3D rect, Action<T> action) where T : Component
        //{
        //    var list = Tracker.Components[typeof(T)];

        //    for (int i = 0; i < list.Count; i++)
        //        if (list[i].Entity.Collidable && list[i].Entity.CollideRect(rect))
        //            action(list[i] as T);
        //}

        //public Vector3 LineWalkCheckByComponent<T>(Vector3 from, Vector3 to, float precision) where T : Component
        //{
        //    Vector3 add = to - from;
        //    add.Normalize();
        //    add *= precision;

        //    int amount = (int)Math.Floor((from - to).Length() / precision);
        //    Vector3 prev = from;
        //    Vector3 at = from + add;

        //    for (int i = 0; i <= amount; i++)
        //    {
        //        if (CollideCheckByComponent<T>(at))
        //            return prev;
        //        prev = at;
        //        at += add;
        //    }

        //    return to;
        //}

        #endregion

        #region Utils

        internal void SetActualDepth(Entity entity)
        {
            //Mark lists unsorted
            Entities.MarkUnsorted();
            for (int i = 0; i < BitTag.TotalTags; i++)
                if (entity.TagCheck((uint)(1 << i)))
                    TagLists.MarkUnsorted(i);
        }

        #endregion

        #region Entity Shortcuts

        /// <summary>
        /// Shortcut to call Engine.Pooler.Create, add the Entity to this Scene, and return it. Entity type must be marked as Pooled
        /// </summary>
        /// <typeparam name="T">Pooled Entity type to create</typeparam>
        /// <returns></returns>
        public T CreateAndAdd<T>() where T : Entity, new()
        {
            var entity = Engine.Pooler.Create<T>();
            Add(entity);
            return entity;
        }

        /// <summary>
        /// Quick access to entire tag lists of Entities. Result will never be null
        /// </summary>
        /// <param name="tag">The tag list to fetch</param>
        /// <returns></returns>
        public List<Entity> this[BitTag tag]
        {
            get
            {
                return TagLists[tag.ID];
            }
        }

        /// <summary>
        /// Shortcut function for adding an Entity to the Scene's Entities list
        /// </summary>
        /// <param name="entity">The Entity to add</param>
        public void Add(Entity entity)
        {
            Entities.Add(entity);
        }

        /// <summary>
        /// Shortcut function for removing an Entity from the Scene's Entities list
        /// </summary>
        /// <param name="entity">The Entity to remove</param>
        public void Remove(Entity entity)
        {
            Entities.Remove(entity);
        }

        /// <summary>
        /// Shortcut function for adding a set of Entities from the Scene's Entities list
        /// </summary>
        /// <param name="entities">The Entities to add</param>
        public void Add(IEnumerable<Entity> entities)
        {
            Entities.Add(entities);
        }

        /// <summary>
        /// Shortcut function for removing a set of Entities from the Scene's Entities list
        /// </summary>
        /// <param name="entities">The Entities to remove</param>
        public void Remove(IEnumerable<Entity> entities)
        {
            Entities.Remove(entities);
        }

        /// <summary>
        /// Shortcut function for adding a set of Entities from the Scene's Entities list
        /// </summary>
        /// <param name="entities">The Entities to add</param>
        public void Add(params Entity[] entities)
        {
            Entities.Add(entities);
        }

        /// <summary>
        /// Shortcut function for removing a set of Entities from the Scene's Entities list
        /// </summary>
        /// <param name="entities">The Entities to remove</param>
        public void Remove(params Entity[] entities)
        {
            Entities.Remove(entities);
        }

        /// <summary>
        /// Allows you to iterate through all Entities in the Scene
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Entity> GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        public IEnumerable<T> GetAll<T>() where T : Entity {
            foreach (var e in Entities.GetAll()) {
                if (e is T)
                    yield return e as T;
            }
            yield break;
        }

        public IEnumerable<Entity> IGetAll<T>() {
            foreach (var e in Entities.GetAll()) {
                if (e is T)
                    yield return e;
            }
            yield break;
        }

        public IEnumerable<T> GetAll<T>(uint flags) where T : Entity {
            foreach (var e in Entities.GetAll(flags)) {
                if (e is T)
                    yield return e as T;
            }
            yield break;
        }

        public IEnumerable<Entity> IGetAll<T>(uint flags) {
            foreach (var e in Entities.GetAll(flags)) {
                if (e is T)
                    yield return e;
            }
            yield break;
        }

        /// <summary>
        /// Allows you to iterate through all Entities in the Scene
        /// </summary>
        /// <returns></returns>
        IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<Entity> GetEntitiesByTagMask(int mask)
        {
            List<Entity> list = new List<Entity>();
            foreach (var entity in Entities)
                if ((entity.Tag & mask) != 0)
                    list.Add(entity);
            return list;
        }

        public List<Entity> GetEntitiesExcludingTagMask(int mask)
        {
            List<Entity> list = new List<Entity>();
            foreach (var entity in Entities)
                if ((entity.Tag & mask) == 0)
                    list.Add(entity);
            return list;
        }

		#endregion

		#region Renderer Shortcuts

		/// <summary>
		/// Shortcut function to add a Renderer to the Renderer list
		/// </summary>
		/// <param name="renderer">The Renderer to add</param>
		public void Add(Renderer renderer)
        {
            RendererList.Add(renderer);
        }

        /// <summary>
        /// Shortcut function to remove a Renderer from the Renderer list
        /// </summary>
        /// <param name="renderer">The Renderer to remove</param>
        public void Remove(Renderer renderer)
        {
            RendererList.Remove(renderer);
        }

        #endregion
    }
}
