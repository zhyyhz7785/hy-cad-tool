using System.IO;
using Newtonsoft.Json;

namespace HyCAD.BlenderUI.Screen.Areas
{
    public static class AreaTreeSerializer
    {
        public static void Save(AreaTreeNode root, string path)
        {
            var json = JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
            File.WriteAllText(path, json);
        }

        public static AreaTreeNode Load(string path)
        {
            if (!File.Exists(path)) return new AreaLeaf();
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AreaTreeNode>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }) ?? new AreaLeaf();
        }
    }
}
