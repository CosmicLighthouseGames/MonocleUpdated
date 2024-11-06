using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public class ModelRenderCall : IDrawCall {
		public Material material {
			set {
				materials = new Material[] { value };
			}
		}
		public MonocleVertex[] vertices;
		public short[][] indices;
		public Material[] materials;
		public Matrix transform;

		public void Render(GraphicsDevice device) {

			Material mat = null;
			var drawcall = this;

			void SetMaterial(Material newMat) {
				if (mat == newMat) {
					return;
				}
				mat = newMat;

				var tech = mat.Technique;
				var techPass = tech.Passes[0];

				if (mat.Stencil != device.DepthStencilState.ReferenceStencil) {
					var dsMask = new DepthStencilState();
					dsMask.ReadFrom(device.DepthStencilState);
					dsMask.ReferenceStencil = mat.Stencil;
					device.DepthStencilState = dsMask;
				}

				var tex = mat.Texture??Draw.Pixel;
				var pData = mat.parameterData;

				Draw.SetParameters(mat.BaseEffect, (param) => {
					switch (param.Name) {
						case "DiffuseColor":
							param.SetValue(mat.Color.ToVector4());
							return true;
						case "Texture":
							param.SetValue(tex.Texture);
							return true;
						case "World":
							param.SetValue(drawcall.transform);
							return true;
						default:
							if (pData.ContainsKey(param.Name)) {
								var data = pData[param.Name];
								if (data is MTexture)
									param.SetValue(data.Texture);
								else if (data is Color)
									param.SetValue(data.ToVector4());
								else if (data is int || data is float) {
									if (param.ParameterType is EffectParameterType.Single) {
										param.SetValue((float)pData[param.Name]);
									}
									else {
										param.SetValue((int)pData[param.Name]);
									}
								}
								else {
									param.SetValue(pData[param.Name]);
								}
								return true;
							}
							return false;
					}
				});

				techPass.Apply();
			}

			for (int i = 0; i < indices.Length; i++) {
				if (indices[i].Length <= 0)
					continue;

				Material newMat;
				if (Draw.OverridingMaterial != null) {
					newMat = Draw.OverridingMaterial;
				}
				else if (i >= materials.Length) {
					newMat = materials[0];
				}
				else {
					newMat = materials[i];
				}
				SetMaterial(newMat);
				device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices[i], 0, indices[i].Length / 3);
			}
		}
	}
	public class BasicModelRenderer : GraphicsComponent {

		MonocleModel mesh;
		public MonocleModel Mesh {
			get => mesh;

			set {
				mesh = value;
				if (mesh != null) {
					Material[] mats = new Material[mesh.MaterialCount];
					int i = 0;
					if (materials != null) {
						for (i = 0; i < Math.Min(mats.Length, materials.Length); i++) {
							mats[i] = materials[i];
						}
					}
					for (; i < mats.Length; i++) {
						mats[i] = Draw.DefaultMaterial;
					}
					materials = mats;
				}
			}
		}
		public Material Material {
			get {
				return materials[0];
			}
			set {
				if (materials == null) {
					materials = new Material[] { value };
				}
				else {
					materials[0] = value;
				}
			}
		}
		private Material[] materials;

		public Material this[int i] {
			get {
				return materials[i];
			}
			set {
				materials[i] = value;
			}
		}

		public BasicModelRenderer Parent;


		private Matrix GlobalTransform() {

			Matrix mat;

			if (OverrideMatrix != null) {
				mat = OverrideMatrix.Value;
			}
			else {
				mat = Matrix.CreateScale(Scale) * Matrix.CreateFromQuaternion(Rotation);
			}

			if (Parent != null) {
				mat = Parent.GlobalTransform() * mat;
			}

			return mat;
		}
		private Vector3 GlobalOffset(Vector3 pos) {


			if (Parent != null) {
				Matrix mat = Parent.GlobalTransform();
				Vector3 parentPos = Parent.GlobalOffset(Parent.Position);
				pos = Vector3.Transform(pos, mat);
				pos += parentPos;
			}
			else if (Entity != null) {
				pos += Entity.Position;
			}

			return pos;
		}
		public Matrix TransMatrix() {

			Vector3 globalOffset = GlobalOffset(Position);


			var mat = GlobalTransform();

			mat *= Matrix.CreateTranslation(globalOffset);

			return mat;
		}

		public BasicModelRenderer() : base(true) {

			Rotation = Quaternion.Identity;
			Scale = Vector3.One;
		}

		public override void Update() {
			base.Update();
		}

		public override void Render() {
			base.Render();

			if (Mesh == null || materials == null)
				return;

			Vector3 position = Position;


			Draw.CustomDrawCall(new ModelRenderCall() {
				vertices = Mesh.vertices,
				indices = Mesh.indices,
				materials = materials,
				transform = TransMatrix()
			});

		}

		public BasicModelRenderer SetMaterial(Material material, int index = 0) {
			materials[index] = material;
			return this;
		}
		public BasicModelRenderer SetMaterials(params Material[] material) {
			if (materials == null) {
				materials = new Material[material.Length];
			}
			for (int i = 0; i < Math.Min(materials.Length, material.Length); i++) {
				materials[i] = material[i];
			}
			return this;
		}
		public BasicModelRenderer CopyMaterial(Material material, int index = 0) {
			materials[index] = new Material(material);
			return this;
		}
		public BasicModelRenderer CopyMaterials(params Material[] material) {
			if (materials == null) {
				materials = new Material[material.Length];
			}
			for (int i = 0; i < Math.Min(materials.Length, material.Length); i++) {
				materials[i] = new Material(material[i]);
			}
			return this;
		}
		public BasicModelRenderer SetMesh(MonocleModel mesh) {
			this.mesh = mesh;
			return this;
		}
	}
}
