using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Monocle {
	public delegate void TransformVertex(int index, ref MonocleVertex vertex);
	public unsafe class ModelRenderCall : IDrawCall {
		static MonocleVertex[] buffer = new MonocleVertex[0x2000];

		public Material material;
		public MonocleVertex[] vertices;
		public short[] indices;
		public Matrix transform;
		public DepthStencilState DepthStencilState;

		public List<TransformVertex> modifiers;


		public int RenderOrder { get; set; }

		public void Render(GraphicsDevice device) {

			if (indices.Length == 0)
				return;

			if (modifiers != null) {

				fixed (MonocleVertex* meshPtr = vertices) {
					fixed (MonocleVertex* bufferPtr = buffer) {
						for (int i = 0; i < vertices.Length; i++) {
							var vert = meshPtr[i];

							foreach (var t in modifiers) {
								t(i, ref vert);
							}

							bufferPtr[i] = vert;
						}
					}
				}
			}
			else {
				fixed (MonocleVertex* meshPtr = vertices) {
					fixed (MonocleVertex* bufferPtr = buffer) {
						for (int i = 0; i < vertices.Length; i++) {
							var vert = meshPtr[i];

							bufferPtr[i] = vert;
						}
					}
				}
			}

			Material mat = null;
			var drawcall = this;

			void SetMaterial(Material newMat) {
				if (mat == newMat) {
					return;
				}
				mat = newMat;

				var tech = mat.Technique;
				var techPass = tech.Passes[0];

				var stencil = DepthStencilState??mat.DepthStencilState??Draw.FallbackDepthState;
				device.DepthStencilState = stencil;

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
							try {
								if (pData.ContainsKey(param.Name)) {
									var data = pData[param.Name];
									if (data != null) {

										if (data is MTexture)
											param.SetValue(data.Texture);
										else if (data is Color)
											param.SetValue(data.ToVector4());
										else if (param.ParameterType == EffectParameterType.Single && param.RowCount == 1 && param.ColumnCount == 1)
											param.SetValue(Convert.ToSingle(data));
										else if (param.ParameterType == EffectParameterType.Int32)
											param.SetValue(Convert.ToInt32(data));
										else
											param.SetValue(pData[param.Name]);
									}
									return true;
								}
							}
							catch {

							}
							return false;
					}
				});

				techPass.Apply();
			}

			Material newMat;
			if (Draw.OverridingMaterial != null) {
				newMat = Draw.OverridingMaterial;
			}
			else {
				newMat = material;
			}
			SetMaterial(newMat);
			device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, buffer, 0, vertices.Length, indices, 0, indices.Length / 3);

		}
	}

	public class ModelRenderer : GraphicsComponent {

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
		public Material[] Materials {
			get {
				return materials;
			}
		}
		protected Material[] materials;

		public Material this[int i] {
			get {
				return materials[i];
			}
			set {
				materials[i] = value;
			}
		}

		public ModelRenderer Parent;

		public List<TransformVertex> Transforms = new List<TransformVertex>();

		public MonocleArmature Armature;


		private Matrix GlobalTransform() {

			Matrix mat;

			if (OverrideMatrix != null) {
				mat = OverrideMatrix.Value;
			}
			else {
				mat = Matrix.Identity;
				mat = Matrix.CreateScale(Scale) * Matrix.CreateFromQuaternion(Rotation);
			}

			if (Parent != null) {
				mat = mat * Parent.GlobalTransform();
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

		public ModelRenderer() : base(true) {

			Rotation = Quaternion.Identity;
			Scale = Vector3.One;
		}

		public override void Update() {
			base.Update();
		}

		public override void Render() {
			base.Render();
			
			if (Mesh == null)
				return;

			List<TransformVertex> transforms = new List<TransformVertex>(Transforms);

			if (Armature != null) {
				transforms.AddRange(Armature.Transform(mesh));
			}

			for (int i = 0; i < Mesh.indices.Length; i++) {
				var mat = materials.Length > 0 ? ((i < materials.Length) ? materials[i] : materials[0]) : Draw.DefaultMaterial;
				Draw.CustomDrawCall(new ModelRenderCall() {
					vertices = Mesh.vertices,
					indices = Mesh.indices[i],
					material = mat,
					modifiers = transforms,
					transform = TransMatrix(),
					RenderOrder = mat.RenderOrder??Draw.CurrentRenderOrder,
				});
			}

		}

		public ModelRenderer SetMaterial(Material material, int index = 0) {
			materials[index] = material;
			return this;
		}
		public ModelRenderer SetMaterials(params Material[] material) {
			if (materials == null) {
				materials = new Material[material.Length];
			}
			for (int i = 0; i < Math.Min(materials.Length, material.Length); i++) {
				materials[i] = material[i];
			}
			return this;
		}
		public ModelRenderer CopyMaterial(Material material, int index = 0) {
			materials[index] = new Material(material);
			return this;
		}
		public ModelRenderer CopyMaterials(params Material[] material) {
			if (materials == null) {
				materials = new Material[material.Length];
			}
			for (int i = 0; i < Math.Min(materials.Length, material.Length); i++) {
				materials[i] = new Material(material[i]);
			}
			return this;
		}
		public ModelRenderer SetMesh(MonocleModel mesh) {
			this.Mesh = mesh;
			return this;
		}
	}
}
