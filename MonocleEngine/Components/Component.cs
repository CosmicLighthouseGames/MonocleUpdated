using Microsoft.Xna.Framework;
using System;
using System.Reflection.Metadata.Ecma335;

namespace Monocle
{
    public class Component : IMonocleRenderer
    {
        public Entity Entity { get; private set; }
        public bool Active;
        public bool Visible;

        int? RenderOrderLocal;

		public int RenderOrder {
			get {
				if (RenderOrderLocal != null)
					return RenderOrderLocal.Value;
				if (Entity != null)
					return Entity.RenderOrder;
				return 0;
			}
			set {
				RenderOrderLocal = value;
			}
		}



		private uint tag;

		public Component(bool active, bool visible)
        {
            Active = active;
            Visible = visible;
        }

        public virtual void Added(Entity entity)
        {
            Entity = entity; 
            if (Scene != null)
                Scene.Tracker.ComponentAdded(this);
        }

        public virtual void Removed(Entity entity)
        {
            if (Scene != null)
                Scene.Tracker.ComponentRemoved(this);
            Entity = null;
        }

        public virtual void EntityAdded(Scene scene)
        {
            if (tag == 0) {
                tag = Entity.Tag;
            }
            scene.Tracker.ComponentAdded(this);
        }

        public virtual void EntityRemoved(Scene scene)
        {
            scene.Tracker.ComponentRemoved(this);
        }

        public virtual void SceneEnd(Scene scene)
        {

        }

        public virtual void EntityAwake()
        {

        }

        public virtual void Update()
        {

        }

		public virtual void BeforeRender() {

		}

		public virtual void Render()
        {

        }

        public virtual void RenderUI()
        {

        }

        public virtual void DebugRender(Camera camera)
        {

        }

        public virtual void HandleGraphicsReset()
        {

        }

        public virtual void HandleGraphicsCreate()
        {

        }

        public void RemoveSelf()
        {
            if (Entity != null)
                Entity.Remove(this);
        }

        public T SceneAs<T>() where T : Scene
        {
            return Scene as T;
        }

        public T EntityAs<T>() where T : Entity
        {
            return Entity as T;
        }

        public Scene Scene
        {
            get { return Entity.Scene; }
		}

		#region Tag

		public uint Tag {
			get {
				return tag;
			}

			set {
				if (tag != value) {

					tag = value;
				}
			}
		}

		public bool TagFullCheck(uint tag) {
			return (this.tag & tag) == tag;
		}

		public bool TagCheck(uint tag) {
			return (this.tag & tag) != 0;
		}

		public void AddTag(uint tag) {
			Tag |= tag;
		}

		public void RemoveTag(uint tag) {
			Tag &= ~tag;
		}

		#endregion

	}
}
