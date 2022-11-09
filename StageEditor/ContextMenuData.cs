using Haven.Parser;
using Haven.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven
{
    public class ContextMenuData
    {
        private static Dictionary<object, ContextMenuData> ContextMenuDataLookup = new Dictionary<object, ContextMenuData>();

        public TreeNode Node;
        public Mesh Mesh;
        public GeomProp? PropOriginal;
        public GeomProp? Prop;

        public ContextMenuData(object obj, TreeNode node, Mesh mesh, GeomProp? prop)
        {
            Node = node;
            Mesh = mesh;
            Prop = prop;

            if (prop != null)
            {
                var propOriginal = new GeomProp();
                propOriginal.X = prop.X;
                propOriginal.Y = prop.Y;
                propOriginal.Z = prop.Z;
                PropOriginal = propOriginal;
            }

            ContextMenuDataLookup[obj] = this;
        }

        public static void Clear()
        {
            ContextMenuDataLookup.Clear();
        }

        public static ContextMenuData? FromObject(object? obj)
        {
            if (obj == null)
                return null;

            ContextMenuData? data;
            ContextMenuDataLookup.TryGetValue(obj, out data);
            return data;
        }
    }
}
