using System.Collections.Generic;
using System.IO;

namespace Monocle
{
    public class ModelAtlas
    {
        class AtlasData : MultiObjectAtlas<FBXScene>
        {
            public FBXScene this[string key] => GetHighestValue(key).Value;
        }
        public static ModelAtlas FromAssetLoader(string contentFolder)
        {

            contentFolder = contentFolder.Replace('\\', '/');

            var atlas = new ModelAtlas();
            atlas.data = new AtlasData();

            foreach (var item in AssetLoader.GetContentInFolder(contentFolder))
            {

                FBXScene models = null;
                string filepath = null;

                switch (item.Extention)
                {
                    case ".fbx":
                        {
                            // make nice for dictionary
                            filepath = Path.ChangeExtension(item.Path, null);
                            filepath = filepath.Replace('\\', '/');
                            filepath = filepath.Substring(contentFolder.Length + 1);

                            if (atlas.data.ContainsKey(filepath))
                                break;

                            models = FBXScene.Import(item);
                        }
                        break;
                    default:
                        continue;
                }
                if (models == null)
                    continue;

                // load
                atlas.data.Add(filepath, models, item.PackMetaData.IsAssetPack ? item.PackMetaData.Priority : int.MinValue);
            }

            return atlas;
        }

        AtlasData data;

        ModelAtlas()
        {

        }

        public Dictionary<string, MonocleModel> CreateMeshes(string path)
        {
            path = path.Replace('\\', '/');
            return data[path].CreateMeshes();
        }
        public MonocleModel CreateMesh(string path)
        {
            path = path.Replace('\\', '/');
            string scene = path.Substring(0, path.IndexOf('.'));
            string mesh = path.Substring(path.IndexOf('.') + 1);
            return data[scene].CreateMesh(mesh);
        }
        public MonocleModel CreateMesh(string path, string name)
        {
            return data[path].CreateMesh(name);
        }
        public MonocleArmature CreateArmature(string path)
        {
            path = path.Replace('\\', '/');
            string scene = path.Substring(0, path.IndexOf('.'));
            string arm = path.Substring(path.IndexOf('.') + 1);
            return data[scene].CreateArmature(arm);
        }
        public MonocleArmature CreateArmature(string path, string name)
        {
            return data[path].CreateArmature(name);
        }

    }
}
