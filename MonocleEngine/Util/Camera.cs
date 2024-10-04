﻿
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Monocle {
	[Tracked(true)]
	public class Camera : Entity {
		static Camera() {

		}

		public static Camera Main { get; set; }

		Matrix? matrix;

		public Matrix? OverrideMatrix {
			get => matrix;
			set {
				if (matrix != value) {
					matrix = value;
					matricesDirty = true;
				}
			}
		}
		public RenderTarget2D[] RenderTargets { get; private set; }
		RenderTargetBinding[] bindings;

		bool windowSize = false;

		Matrix meshMatrix = Matrix.Identity, meshInverse = Matrix.Identity;

		bool matricesDirty;

		bool ortho;
		public bool Orthographic {
			get => ortho;
			set {
				if (value != ortho) {
					ortho = value;
					matricesDirty = true;
				}
			}
		}

		public float OrthoSize;

		float angle;
		public float Angle {
			get => angle;
			set {
				if (angle != value) {
					angle = value;
					matricesDirty = true;
				}
			}
		}

		float near = 0.1f, far = 10_000;
		public float Near {
			get => near;
			set {
				if (near != value) {
					near = value;
					matricesDirty = true;
				}
			}
		}
		public float Far {
			get => far;
			set {
				if (far != value) {
					far = value;
					matricesDirty = true;
				}
			}
		}

		public RasterizerState R_State = RasterizerState.CullClockwise;
		public BlendState B_State = BlendState.NonPremultiplied;
		public DepthStencilState DS_State= DepthStencilState.Default;

		public Color? backgroundColor;

		private Quaternion rotation = Quaternion.Identity;

		public Viewport ViewSize {
			get => viewSize;
			set {
				if (bindings == null) {
					viewSize = value;
				}
			}
		}
		public Viewport Viewport;
		Viewport viewSize;

		Vector3 oldPosition;

		public Camera() : this(-1, -1) {
		}

		public Camera(params RenderTarget2D[] targets) : this(targets[0].Width, targets[0].Height) {
			RenderTargets = targets;
			Viewport = new Viewport(0, 0, viewSize.Width, viewSize.Height);

			bindings = new RenderTargetBinding[targets.Length];
			for (int i = 0; i < RenderTargets.Length; i++) {
				bindings[i] = RenderTargets[i];
			}
		}

		public Camera(int width, int height) {
			Visible = false;
			if (Main == null) {
				Main = this;
			}
			if (width < 0 || height < 0)
				windowSize = true;

			viewSize = new Viewport();
			viewSize.Width =  windowSize ? Engine.WindowWidth : width;
			viewSize.Height = windowSize ? Engine.WindowHeight : height;
			Viewport = new Viewport(0, 0, Engine.WindowWidth, Engine.WindowHeight);

			ortho = false;
			angle = 45;

			UpdateMatrices();

			OrthoSize = Viewport.Height / (float)Engine.PixelsPerUnit;
		}

		public void UpdateMatrices() {

			if (OverrideMatrix != null) {
				meshMatrix = OverrideMatrix.Value;
			}
			else if (ortho) {
				float ratioW = 2 * (Viewport.Width / (float)viewSize.Width) * Engine.WindowWidth / (float)(OrthoSize * viewSize.Width / viewSize.Height);
				float ratioH = 2 * (Viewport.Width / (float)viewSize.Width) * Engine.WindowHeight / (float)OrthoSize;

				float min = Math.Min(ratioW, ratioH);

				meshMatrix =
					Matrix.CreateTranslation(0, 0.25f, 0) *
					Matrix.CreateTranslation(-Position) *
					Matrix.CreateFromQuaternion(Quaternion.Inverse(Rotation)) *
					Matrix.CreateTranslation(0, 0, -near) *
					Matrix.CreateScale(min / Viewport.Width, min / Viewport.Height, -1 / (far - near)) *
					Matrix.Identity
					;

			}
			else {
				meshMatrix =
					Matrix.CreateTranslation(-Position)
					* Matrix.CreateFromQuaternion(Quaternion.Inverse(Rotation))
					* Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(angle), (float)Viewport.Width / Viewport.Height, near, far)
					* Matrix.CreateScale(1.0f / 1, 1.0f / 1, 1 / (far - near))
					;
			}

			meshInverse = Matrix.Invert(meshMatrix);

			matricesDirty = false;
		}

		public void CopyFrom(Camera other) {
			Position = other.Position;
			rotation = other.rotation;
			matricesDirty = true;
		}

		public Matrix Matrix3D {
			get {
				if (matricesDirty)
					UpdateMatrices();
				return meshMatrix;
			}
		}

		public Matrix MatrixInverse {
			get {
				if (matricesDirty)
					UpdateMatrices();
				return meshInverse;
			}
		}

		public Quaternion Rotation {
			get { return rotation; }
			set {
				matricesDirty = true;
				rotation = value;
			}
		}


		public override void HandleGraphicsReset() {
			if (windowSize) {
				viewSize.Width =  Engine.WindowWidth;
				viewSize.Height = Engine.WindowHeight;

				if (bindings != null) {
					for (int i = 0; i < bindings.Length; i++) {
						RenderTargets[i] = new RenderTarget2D(Draw.GraphicsDevice, (int)(Engine.UnitWidth * Engine.PixelsPerUnit), (int)(Engine.UnitHeight * Engine.PixelsPerUnit), false, RenderTargets[i].Format, RenderTargets[i].DepthStencilFormat);
						bindings[i] = RenderTargets[i];
					}
				}
			}
			if (bindings == null) {
				Viewport.Width =  Engine.WindowWidth;
				Viewport.Height = Engine.WindowHeight;
			}
			UpdateMatrices();
		}

		/*
		 *  Utils
		 */

		public Vector3 ScreenToCamera(Vector2 position) {
			return ScreenToCamera(position, 1);
		}

		public Vector3 ScreenToCamera(Vector2 position, float depth) {
			return Vector3.Transform(new Vector3(position, depth), MatrixInverse);
		}

		public Vector2 WorldToScreen(Vector3 position) {
			if (matricesDirty)
				UpdateMatrices();
			var transform = Vector3.Transform(position, Matrix3D).XZ();
			transform.Y *= -1;

			transform += Vector2.One;
			//transform *= 0.5f * new Vector2(Engine.UnitWidth, Engine.UnitHeight);
			return transform;
		}

		public Func<Entity, bool> DoesRender;
		public Func<Component, bool> DoesRenderComponent;

		public void SetRenderTargets(params RenderTarget2D[] textures) {
			if (textures == null) {

			}
			else if (windowSize) {

				for (int i = 0; i < textures.Length; i++) {
					RenderTargets[i] = new RenderTarget2D(Draw.GraphicsDevice, (int)(Engine.UnitWidth * Engine.PixelsPerUnit), (int)(Engine.UnitHeight * Engine.PixelsPerUnit), false, RenderTargets[i].Format, RenderTargets[i].DepthStencilFormat);
					bindings[i] = RenderTargets[i];
				}
			}
			else {
				List<RenderTarget2D> list = new List<RenderTarget2D>();

				int newWidth = textures[0].Width,
					newHeight = textures[0].Height;
				list.Add(textures[0]);

				for (int i = 1; i < textures.Length; i++) {
					if (textures[i].Width == newWidth && textures[i].Height == newHeight) {
						list.Add(textures[i]);
					}
				}

				RenderTargets = list.ToArray();
				bindings = new RenderTargetBinding[RenderTargets.Length];

				for (int i = 0; i < list.Count; i++) {
					bindings[i] = list[i];
				}

				viewSize.Width = newWidth;
				viewSize.Height = newHeight;
			}
		}

		public override void Update() {
			base.Update();
		}

		public void RenderCamera() {
			RenderCameraWithout(0);
		}
		public void RenderCameraWith(uint mask) {

			Func<Entity, bool> renderEntity;
			Func<Component, bool> renderComponent;


			if (DoesRender == null) {
				renderEntity = (ent) => {
					return ent.Visible;
				};
			}
			else {
				renderEntity = (ent) => {
					return ent.Visible && DoesRender(ent);
				};
			}
			if (DoesRenderComponent == null) {
				renderComponent = (comp) => {
					return comp.Visible && comp.TagCheck(mask);
				};
			}
			else {
				renderComponent = (comp) => {
					return comp.Visible && comp.TagCheck(mask) && DoesRenderComponent(comp);
				};
			}

			List<IMonocleRenderer> list = new List<IMonocleRenderer>();

			foreach (var ent in Scene.Entities) {
				if (renderEntity(ent)) {
					if (ent.TagCheck(mask))
						list.Add(ent);
					foreach (var comp in ent.Components) {
						if (renderComponent(comp)) {
							list.Add(comp);
						}
					}
				}
			}

			list.Sort((a, b) => {
				return a.RenderOrder - b.RenderOrder;
			});

			if (list.Count > 0)
				Render(list);
		}
		public void RenderCameraWithout(uint mask) {

			Func<Entity, bool> renderEntity;
			Func<Component, bool> renderComponent;


			if (DoesRender == null) {
				renderEntity = (ent) => {
					return ent.Visible;
				};
			}
			else {
				renderEntity = (ent) => {
					return ent.Visible && DoesRender(ent);
				};
			}
			if (DoesRenderComponent == null) {
				renderComponent = (comp) => {
					return comp.Visible && !comp.TagCheck(mask);
				};
			}
			else {
				renderComponent = (comp) => {
					return comp.Visible && !comp.TagCheck(mask) && DoesRenderComponent(comp);
				};
			}


			List<IMonocleRenderer> list = new List<IMonocleRenderer>();

			foreach (var ent in Scene.Entities) {
				if (renderEntity(ent)) {
					if (!ent.TagCheck(mask))
						list.Add(ent);
					foreach (var comp in ent.Components) {
						if (renderComponent(comp)) {
							list.Add(comp);
						}
					}
				}
			}

			list.Sort((a, b) => {
				return a.RenderOrder - b.RenderOrder;
			});

			if (list.Count > 0)
				Render(list);
		}

		public void Render(IEnumerable<IMonocleRenderer> render) {

			var graphics = Draw.GraphicsDevice;

			Main = this;

			if (bindings == null || bindings.Length == 1) {
				graphics.SetRenderTargets(bindings);

				if (backgroundColor != null)
					graphics.Clear(backgroundColor.Value);
			}
			else {
				graphics.SetRenderTargets(bindings);

				if (backgroundColor != null) {
					graphics.Clear(Color.Transparent);
					graphics.SetRenderTargets(bindings[0]);
					graphics.Clear(backgroundColor.Value);
					graphics.SetRenderTargets(bindings);

				}
			}

			if (Position != oldPosition || matricesDirty) {
				oldPosition = Position;
				UpdateMatrices();
			}

			foreach (var obj in render) {
				obj.Render();
			}

			graphics.RasterizerState = R_State;
			graphics.BlendState = B_State;
			graphics.DepthStencilState = DS_State;

			graphics.Viewport = Viewport;


			Draw.WorldProjection = Matrix3D;
			Draw.RenderPass();

			Draw.ClearGraphics();
		}
	}
}