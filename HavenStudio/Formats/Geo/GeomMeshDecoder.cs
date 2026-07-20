using System;
using System.Collections.Generic;
using HavenStudio.Utils;

namespace HavenStudio.Formats.Geo;

/// <summary>
/// Render/exchange-ready collision triangles decoded entirely from the parsed GEOM
/// structures. Vertices are split per source primitive/polygon so triangle metadata
/// remains stable for selection and glTF exchange.
/// </summary>
public sealed class GeomDecodedMesh
{
    internal GeomDecodedMesh(
        float[] positions,
        uint[] indices,
        int[] primitiveIndices,
        int[] polygonIndices,
        int[] sourceVertexIndices)
    {
        Positions = positions;
        Indices = indices;
        PrimitiveIndices = primitiveIndices;
        PolygonIndices = polygonIndices;
        SourceVertexIndices = sourceVertexIndices;
    }

    public float[] Positions { get; }
    public uint[] Indices { get; }
    public int[] PrimitiveIndices { get; }
    public int[] PolygonIndices { get; }
    public int[] SourceVertexIndices { get; }
    public int VertexCount => Positions.Length / 3;
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>
/// Shared GEOM polygon decoder used by rendering and mesh exchange. It deliberately
/// has no dependency on the source stream, which may already be closed after loading.
/// </summary>
public static class GeomMeshDecoder
{
    public static bool TryDecodeBlock(
        GeoVertexHeader vertexData,
        IReadOnlyList<Geom> faceData,
        out GeomDecodedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(vertexData);
        ArgumentNullException.ThrowIfNull(faceData);

        mesh = new GeomDecodedMesh([], [], [], [], []);
        if (vertexData.Data.Length == 0 ||
            vertexData.PositionStart < 0 ||
            vertexData.PositionStart >= vertexData.Data.Length ||
            vertexData.VertexStart < 0 ||
            vertexData.VertexStart >= vertexData.Data.Length)
        {
            return false;
        }

        var basePosition = vertexData.Data[vertexData.PositionStart];
        var vertexCount = vertexData.Data.Length - vertexData.VertexStart;
        var sourcePositions = new float[checked(vertexCount * 3)];
        for (var sourceIndex = 0; sourceIndex < vertexCount; sourceIndex++)
        {
            var vertex = vertexData.Data[vertexData.VertexStart + sourceIndex];
            var offset = sourceIndex * 3;
            sourcePositions[offset] = vertex.X + basePosition.X;
            sourcePositions[offset + 1] = vertex.Y + basePosition.Y;
            sourcePositions[offset + 2] = vertex.Z + basePosition.Z;
        }

        var renderPositions = new List<float>();
        var indices = new List<uint>();
        var primitiveIndices = new List<int>();
        var polygonIndices = new List<int>();
        var sourceVertexIndices = new List<int>();
        var renderVertexLookup = new Dictionary<(int Source, int Primitive, int Polygon), uint>();

        uint GetRenderVertex(int sourceIndex, int primitiveIndex, int polygonIndex)
        {
            var key = (sourceIndex, primitiveIndex, polygonIndex);
            if (renderVertexLookup.TryGetValue(key, out var renderIndex))
            {
                return renderIndex;
            }

            renderIndex = checked((uint)(renderPositions.Count / 3));
            var sourceOffset = sourceIndex * 3;
            renderPositions.Add(sourcePositions[sourceOffset]);
            renderPositions.Add(sourcePositions[sourceOffset + 1]);
            renderPositions.Add(sourcePositions[sourceOffset + 2]);
            sourceVertexIndices.Add(sourceIndex);
            renderVertexLookup[key] = renderIndex;
            return renderIndex;
        }

        void AddTriangle(
            int a,
            int b,
            int c,
            int primitiveIndex,
            int polygonIndex)
        {
            indices.Add(GetRenderVertex(a - 1, primitiveIndex, polygonIndex));
            indices.Add(GetRenderVertex(b - 1, primitiveIndex, polygonIndex));
            indices.Add(GetRenderVertex(c - 1, primitiveIndex, polygonIndex));
            primitiveIndices.Add(primitiveIndex);
            polygonIndices.Add(polygonIndex);
        }

        for (var primitiveIndex = 0; primitiveIndex < faceData.Count; primitiveIndex++)
        {
            var face = faceData[primitiveIndex];
            if (face.GetPrimType() != Geom.Primitive.GEO_POLY || face.Poly == null)
            {
                continue;
            }

            for (var polygonIndex = 0; polygonIndex < face.Poly.Length; polygonIndex++)
            {
                var polygon = face.Poly[polygonIndex];
                if (polygon.Data.Length < 5)
                {
                    continue;
                }

                var a = polygon.Data[0] + 1;
                var b = polygon.Data[1] + 1;
                var c = polygon.Data[2] + 1;
                var d = polygon.Data[3] + 1;
                GeomUtils.FaceBitCalculation(polygon.Data[4], ref a, ref b, ref c, ref d);

                if (!AreIndicesValid(a, b, c, d, vertexCount))
                {
                    continue;
                }

                AddTriangle(a, b, c, primitiveIndex, polygonIndex);
                AddTriangle(a, c, d, primitiveIndex, polygonIndex);
            }
        }

        if (renderPositions.Count == 0 || indices.Count == 0)
        {
            return false;
        }

        mesh = new GeomDecodedMesh(
            renderPositions.ToArray(),
            indices.ToArray(),
            primitiveIndices.ToArray(),
            polygonIndices.ToArray(),
            sourceVertexIndices.ToArray());
        return true;
    }

    private static bool AreIndicesValid(int a, int b, int c, int d, int vertexCount) =>
        a > 0 && b > 0 && c > 0 && d > 0 &&
        a <= vertexCount && b <= vertexCount && c <= vertexCount && d <= vertexCount;
}
