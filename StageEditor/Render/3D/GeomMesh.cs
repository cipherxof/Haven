using Haven.Parser.Geom;
using Haven.Parser;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.ES11;

namespace Haven.Render
{
    public static class GeomMesh
    {
        public static Dictionary<Mesh, GeoBlock> BlockLookup = new Dictionary<Mesh, GeoBlock>();

        /// <summary>
        /// Generates mesh data from a list of geom indicies
        /// </summary>
        /// <param name="geomFile"></param>
        /// <param name="indicies"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static List<Mesh> GetGeomBlockMeshes(GeomFile geomFile, List<GeoBlock> blocks, Color color, string id = "")
        {

            List<Vector3d> verts = new List<Vector3d>();
            List<int> faces = new List<int>();
            List<Mesh> meshes = new List<Mesh>();

            for (int y = 0; y < blocks.Count; y++)
            {
                var block = blocks[y];

                if (block.VertexOffset == 0 || block.VertexOffset > geomFile.Stream.Length)
                    continue;

                var vertexData = geomFile.BlockVertexData[block];

                geomFile.Stream.Seek(block.VertexOffset + ((vertexData.PositionStart * 0x10) + 0x10), SeekOrigin.Begin);

                var posX = geomFile.Reader.ReadSingle();
                var posY = geomFile.Reader.ReadSingle();
                var posZ = geomFile.Reader.ReadSingle();
                var posW = geomFile.Reader.ReadSingle();

                geomFile.Stream.Seek(block.VertexOffset + ((vertexData.VertexStart * 0x10) + 0x10), SeekOrigin.Begin);

                for (int n = 0; n < vertexData.Data.Length; n++)
                {
                    var vx = geomFile.Reader.ReadSingle();
                    var vy = geomFile.Reader.ReadSingle();
                    var vz = geomFile.Reader.ReadSingle();
                    var vw = geomFile.Reader.ReadUInt32();

                    verts.Add(new Vector3d(vx + posX, vy + posY, vz + posZ));
                }

                var faceData = geomFile.BlockFaceData[block];
                string name = block.VertexOffset.ToString("X4");

                geomFile.Stream.Seek(block.FaceOffset, SeekOrigin.Begin);

                foreach (var face in faceData)
                {
                    if (face.GetPrimType() != Geom.Primitive.GEO_POLY || face.Poly == null)
                        continue;

                    foreach (var poly in face.Poly)
                    {
                        var fa = poly.Data[0] + 1;
                        var fb = poly.Data[1] + 1;
                        var fc = poly.Data[2] + 1;
                        var fd = poly.Data[3] + 1;
                        var extraBit = poly.Data[4];

                        Utils.FaceBitCalculation(extraBit, ref fa, ref fb, ref fc, ref fd);

                        faces.Add(fa - 1);
                        faces.Add(fb - 1);
                        faces.Add(fc - 1);

                        faces.Add(fa - 1);
                        faces.Add(fc - 1);
                        faces.Add(fd - 1);
                    }
                }

                Mesh mesh = new Mesh(verts.ToArray(), faces.ToArray());
                mesh.ID = id != "" ? id : name;
                mesh.SetColor(color, false);
                meshes.Add(mesh);
                verts = new List<Vector3d>();
                faces = new List<int>();

                BlockLookup[mesh] = block;

            }

            return meshes;
        }

        /// <summary>
        /// Generate meshes from a geom file.
        /// </summary>
        /// <param name="geomFile"></param>
        /// <returns></returns>
        public static List<Mesh> GetGeomGroupMeshes(GeomFile geomFile)
        {
            List<Mesh> meshes = new List<Mesh>();

            geomFile.GeomGroups.ForEach(group => {
                meshes.AddRange(GetGeomBlockMeshes(geomFile, geomFile.GeomGroupBlocks[group], Color.Gray));
            });

            //meshes = new List<Mesh>() { CombineMeshes(meshes) };

            return meshes;
        }

        /// <summary>
        /// Generate meshes from a geom file.
        /// </summary>
        /// <param name="geomFile"></param>
        /// <returns></returns>
        public static List<Mesh> GetGeomRefMeshes(GeomFile geomFile)
        {
            List<Mesh> meshes = new List<Mesh>();

            geomFile.GeomRefs.ForEach(obj => meshes.AddRange(GetGeomBlockMeshes(geomFile, geomFile.GeomRefBlocks[obj], Color.Gray, DictionaryFile.GetHashString(obj.Hash))));

            return meshes;
        }
    }
}
