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
	public unsafe struct VertexRenderCall : IDrawCall {
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

			var RenderOrder = this.RenderOrder;
			var DepthStencilState = this.DepthStencilState;

			void SetMaterial(Material newMat) {
				if (mat == newMat) {
					return;
				}
				mat = newMat;

				var tech = mat.GetTechnique(RenderOrder);
				var techPass = tech.Passes[0];

				var stencil = DepthStencilState??mat.DepthStencilState??Draw.FallbackDepthState;
				device.DepthStencilState = stencil;

				var tex = mat.Texture??Draw.Pixel;
				var pData = mat.parameterData;

				mat.SetParameters(drawcall.transform, tex);

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
	public struct ModelRenderCall : IDrawCall {

		public MonocleArmature armature;
		public Material material;
		public int vertexCount;
		public MonocleModelMaterialSlot mesh;
		public Matrix transform;
		public DepthStencilState DepthStencilState;


		public int RenderOrder { get; set; }

		public void Render(GraphicsDevice device) {

			if (mesh == null)
				return;

			Material mat = null;
			var drawcall = this;

			var RenderOrder = this.RenderOrder;
			var DepthStencilState = this.DepthStencilState;

			var model = mesh;


			if (Draw.OverridingMaterial != null) {
				mat = Draw.OverridingMaterial;
			}
			else {
                mat = material;
            }

            var tech = mat.GetTechnique(RenderOrder);

            var stencil = DepthStencilState ?? mat.DepthStencilState ?? Draw.FallbackDepthState;
            device.DepthStencilState = stencil;

            var tex = mat.Texture ?? Draw.Pixel;


            mat.SetParameters(drawcall.transform, tex);

            if (armature != null)
            {
				foreach (var part in mesh)
				{
					for (int i = 0; i < 4; i++)
					{
						if (material.BaseEffect.Parameters[$"Bone{i}"] == null)
							continue;

						if (part.BoneNames[i] == null)
                        {
                            material.BaseEffect.Parameters[$"Bone{i}"].SetValue(Matrix.Identity);
                            continue;
						}

						var bone = armature[part.BoneNames[i]];

                        material.BaseEffect.Parameters[$"Bone{i}"].SetValue(bone.Transform);
					}

                    foreach (var pass in tech.Passes)
                    {
                        pass.Apply();
                        {
                            part.MeshData.SetIndex(part.WeightData);
                            part.MeshData.RenderList();
                        }
                    }
                }
            }
			else
			{
                foreach (var part in mesh)
				{
					foreach (var pass in tech.Passes)
					{
						pass.Apply();
						{
							part.MeshData.SetIndex(part.WeightData);
							part.MeshData.RenderList();
						}
					}
				}
            }




		}
	}
	[Tracked(true)]
	public class ModelRenderer : GraphicsComponent {

        public Material[] Materials { get; private set; }

		MonocleModel mesh;

        public MonocleModel Mesh
		{
			get
			{
				return mesh;
			}
			set
			{
				mesh = value;
				if (mesh == null)
					return;
				var oldMaterials = Materials;
				Materials = new Material[Math.Max(mesh.Count, 1)];
				if (oldMaterials != null)
					SetMaterials(oldMaterials);
			}
		}

        public MonocleArmature Armature;

        public Material Material {
			get {
				return Materials[0];
			}
			set {
				Materials[0] = value;
			}
		}

		public Material this[int i] {
			get {
				return Materials[i];
			}
			set {
                Materials[i] = value;
			}
		}

		public ModelRenderer Parent;

		public List<TransformVertex> Transforms = new List<TransformVertex>();

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
			Materials = new Material[1];
		}

		public override void Update() {
			base.Update();
		}

		public override void Render() {
			base.Render();
			
			if (Mesh == null)
				return;

			Mesh.Render(TransMatrix(), Armature, Materials);

		}

		public ModelRenderer SetMaterial(Material material, int index = 0) {
            Materials[index] = material;
			return this;
		}
		public ModelRenderer SetMaterials(params Material[] material) {
			for (int i = 0; i < Math.Min(Materials.Length, material.Length); i++) {
                Materials[i] = material[i];
			}
			return this;
		}
		public ModelRenderer CopyMaterial(Material material, int index = 0) {
            Materials[index] = new Material(material);
			return this;
		}
		public ModelRenderer CopyMaterials(params Material[] material) {
			for (int i = 0; i < Math.Min(Materials.Length, material.Length); i++) {
                Materials[i] = new Material(material[i]);
			}
			return this;
		}
		public ModelRenderer SetMesh(MonocleModel mesh) {
			Mesh = mesh;
			return this;
		}
	}
}
