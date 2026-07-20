using System;
using System.Collections.Generic;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Geo;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public readonly record struct CollisionTriangleFilterResult(
    uint[] Indices,
    int[] PrimitiveIndices,
    int[] PolygonIndices);

public static class GeomSceneBuilder
{
    public const float CollisionMeshAlpha = 1.0f;

    private const int GridMaxLinesPerAxis = 500;
    private const float GridMinLineWidth = 1.0f;
    private const float GridMaxLineWidth = 200.0f;

    private static readonly Vector3 DefaultCollisionColor = new(0.62f, 0.66f, 0.72f);

    private static readonly (ulong Mask, Vector3 Color)[] CollisionAttributeStyles =
    [
        (GeoCollisionAttributes.Water, new(0.20f, 0.58f, 1.00f)),
        (GeoCollisionAttributes.Stairway, new(1.00f, 0.80f, 0.20f)),
        (GeoCollisionAttributes.Cliff, new(1.00f, 0.38f, 0.28f)),
        (GeoCollisionAttributes.Rail, new(1.00f, 0.58f, 0.18f)),
        (GeoCollisionAttributes.HeightLimit, new(0.90f, 0.40f, 1.00f)),
        (GeoCollisionAttributes.TypeThrough, new(0.18f, 0.86f, 0.88f)),
        (GeoCollisionAttributes.BehindThrough, new(0.20f, 0.72f, 0.62f)),
        (GeoCollisionAttributes.NoBehind, new(0.62f, 0.50f, 1.00f)),
        (GeoCollisionAttributes.DontFall, new(1.00f, 0.46f, 0.72f)),
        (GeoCollisionAttributes.Camera, new(0.66f, 0.42f, 0.94f)),
        (GeoCollisionAttributes.AttackGuard, new(1.00f, 0.28f, 0.34f)),
        (GeoCollisionAttributes.Floor, new(0.30f, 0.86f, 0.42f)),
        (GeoCollisionAttributes.TypeRecoil, new(1.00f, 0.48f, 0.30f)),
        (GeoCollisionAttributes.Player, new(0.28f, 0.68f, 1.00f)),
        (GeoCollisionAttributes.Enemy, new(0.94f, 0.30f, 0.38f)),
        (GeoCollisionAttributes.Bullet, new(1.00f, 0.84f, 0.28f)),
        (GeoCollisionAttributes.Missile, new(1.00f, 0.62f, 0.22f)),
        (GeoCollisionAttributes.Bomb, new(1.00f, 0.40f, 0.22f)),
        (GeoCollisionAttributes.Radar, new(0.22f, 0.82f, 0.82f)),
        (GeoCollisionAttributes.Blood, new(0.78f, 0.16f, 0.28f)),
        (GeoCollisionAttributes.Ik, new(0.72f, 0.54f, 1.00f)),
        (GeoCollisionAttributes.StopEye, new(0.78f, 0.42f, 0.88f)),
        (GeoCollisionAttributes.Lean, new(0.66f, 0.88f, 0.24f)),
        (GeoCollisionAttributes.Shadow, new(0.52f, 0.56f, 0.72f)),
        (GeoCollisionAttributes.Intrude, new(0.92f, 0.42f, 0.72f)),
        (GeoCollisionAttributes.BulletMark, new(0.86f, 0.72f, 0.26f)),
        (GeoCollisionAttributes.Sound, new(0.30f, 0.78f, 0.62f)),
        (GeoCollisionAttributes.Unknown27, new(0.78f, 0.58f, 0.36f)),
        (GeoCollisionAttributes.Unknown28, new(0.48f, 0.78f, 0.32f)),
        (GeoCollisionAttributes.Unknown29, new(0.82f, 0.44f, 0.52f)),
        (GeoCollisionAttributes.Unknown31, new(0.46f, 0.66f, 0.88f)),
        (GeoCollisionAttributes.Unknown32, new(0.82f, 0.58f, 0.86f)),
        (GeoCollisionAttributes.Unknown33, new(0.54f, 0.78f, 0.76f))
    ];

    public static List<Model3D> BuildBlockModels(
        GeomFile geomFile,
        out Dictionary<Model3D, GeoBlock> modelToBlock,
        out Dictionary<GeoBlock, Model3D> blockToModel,
        out Dictionary<Model3D, int[]> trianglePrimIndex,
        out Dictionary<Model3D, int[]> trianglePolyIndex)
    {
        var models = new List<Model3D>();
        modelToBlock = new Dictionary<Model3D, GeoBlock>();
        blockToModel = new Dictionary<GeoBlock, Model3D>();
        trianglePrimIndex = new Dictionary<Model3D, int[]>();
        trianglePolyIndex = new Dictionary<Model3D, int[]>();

        int blockIndex = 0;
        foreach (var block in geomFile.GeomBlocks)
        {
            if (!geomFile.BlockVertexData.TryGetValue(block, out var vertexData))
            {
                blockIndex++;
                continue;
            }

            if (!geomFile.BlockFaceData.TryGetValue(block, out var faceData))
            {
                blockIndex++;
                continue;
            }

            if (!GeomMeshDecoder.TryDecodeBlock(vertexData, faceData, out var mesh))
            {
                blockIndex++;
                continue;
            }

            var positions = mesh.Positions;
            var indices = mesh.Indices;
            var primIndices = mesh.PrimitiveIndices;
            var polyIndices = mesh.PolygonIndices;

            var primitiveAttributes = new ulong[faceData.Count];
            for (var primitiveIndex = 0; primitiveIndex < faceData.Count; primitiveIndex++)
            {
                primitiveAttributes[primitiveIndex] = faceData[primitiveIndex].Attribute;
            }

            var colors = BuildCollisionVertexColors(positions, indices, primIndices, primitiveAttributes);
            var model = new Model3D
            {
                Name = $"GeomBlock_{blockIndex}",
                Positions = positions,
                Colors = colors,
                Indices = indices,
                VertexCount = positions.Length / 3,
                IndexCount = indices.Length,
                Color = new Vector3(0.65f, 0.65f, 0.65f),
                Alpha = CollisionMeshAlpha,
                BlendEnabled = false,
                WriteDepth = true,
                ForceOpaqueAlpha = true,
                DepthBias = -1.0f,
                MaterialIndex = -1
            };

            models.Add(model);
            modelToBlock[model] = block;
            blockToModel[block] = model;
            trianglePrimIndex[model] = primIndices;
            trianglePolyIndex[model] = polyIndices;
            blockIndex++;
        }

        return models;
    }

    public static Model3D? BuildGridModel(GeomFile geomFile)
    {
        if (geomFile.GeomGroups.Count == 0)
        {
            return null;
        }

        var boundaryLow = new Vector4();
        var boundaryHigh = new Vector4();
        geomFile.GetWorldBoundary(ref boundaryLow, ref boundaryHigh);

        float minX = boundaryLow.X;
        float minY = boundaryLow.Y;
        float minZ = boundaryLow.Z;
        float maxX = boundaryHigh.X;
        float maxZ = boundaryHigh.Z;

        if (maxX <= minX || maxZ <= minZ)
        {
            return null;
        }

        float stepX = GetAverageStep(geomFile.GeomGroups, axis: 0);
        float stepZ = GetAverageStep(geomFile.GeomGroups, axis: 2);
        if (stepX <= 0.01f)
        {
            stepX = (maxX - minX) / 20.0f;
        }
        if (stepZ <= 0.01f)
        {
            stepZ = (maxZ - minZ) / 20.0f;
        }

        stepX = AdjustStep(minX, maxX, stepX);
        stepZ = AdjustStep(minZ, maxZ, stepZ);

        float lineWidth = MathF.Min(MathF.Max(MathF.Min(stepX, stepZ) * 0.05f, GridMinLineWidth), GridMaxLineWidth);
        float half = lineWidth * 0.5f;

        var positions = new List<float>();
        var indices = new List<uint>();

        void AddQuad(float x0, float z0, float x1, float z1, bool alongX)
        {
            int baseIndex = positions.Count / 3;
            if (alongX)
            {
                positions.Add(x0); positions.Add(minY); positions.Add(z0 - half);
                positions.Add(x0); positions.Add(minY); positions.Add(z0 + half);
                positions.Add(x1); positions.Add(minY); positions.Add(z1 + half);
                positions.Add(x1); positions.Add(minY); positions.Add(z1 - half);
            }
            else
            {
                positions.Add(x0 - half); positions.Add(minY); positions.Add(z0);
                positions.Add(x0 + half); positions.Add(minY); positions.Add(z0);
                positions.Add(x1 + half); positions.Add(minY); positions.Add(z1);
                positions.Add(x1 - half); positions.Add(minY); positions.Add(z1);
            }

            indices.Add((uint)(baseIndex + 0));
            indices.Add((uint)(baseIndex + 1));
            indices.Add((uint)(baseIndex + 2));
            indices.Add((uint)(baseIndex + 0));
            indices.Add((uint)(baseIndex + 2));
            indices.Add((uint)(baseIndex + 3));
        }

        int linesX = (int)MathF.Floor((maxX - minX) / stepX) + 1;
        for (int i = 0; i < linesX; i++)
        {
            float x = minX + stepX * i;
            AddQuad(x, minZ, x, maxZ, alongX: false);
        }

        int linesZ = (int)MathF.Floor((maxZ - minZ) / stepZ) + 1;
        for (int i = 0; i < linesZ; i++)
        {
            float z = minZ + stepZ * i;
            AddQuad(minX, z, maxX, z, alongX: true);
        }

        if (positions.Count == 0 || indices.Count == 0)
        {
            return null;
        }

        return new Model3D
        {
            Name = "GeomGrid",
            Positions = positions.ToArray(),
            Indices = indices.ToArray(),
            VertexCount = positions.Count / 3,
            IndexCount = indices.Count,
            Color = new Vector3(0.35f, 0.35f, 0.35f),
            Alpha = 0.35f,
            MaterialIndex = -1
        };
    }

    public static float[] BuildVertexColors(float[] positions, uint[] indices, Vector3 baseColor)
    {
        int vertexCount = positions.Length / 3;
        if (vertexCount == 0 || indices.Length == 0)
        {
            return Array.Empty<float>();
        }

        var normals = new Vector3[vertexCount];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i0 = (int)indices[i];
            int i1 = (int)indices[i + 1];
            int i2 = (int)indices[i + 2];

            if (i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
            {
                continue;
            }

            var v0 = ReadPosition(positions, i0);
            var v1 = ReadPosition(positions, i1);
            var v2 = ReadPosition(positions, i2);

            var normal = Vector3.Cross(v1 - v0, v2 - v0);
            if (normal.LengthSquared > 1e-8f)
            {
                normal = normal.Normalized();
            }

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        var lightDir = new Vector3(0.35f, 0.75f, 0.55f).Normalized();
        var colors = new float[vertexCount * 4];
        for (int i = 0; i < vertexCount; i++)
        {
            var normal = normals[i];
            if (normal.LengthSquared > 1e-8f)
            {
                normal = normal.Normalized();
            }

            float ndotl = MathF.Max(0.0f, Vector3.Dot(normal, lightDir));
            float shade = 0.35f + 0.65f * ndotl;
            int dst = i * 4;
            colors[dst] = baseColor.X * shade;
            colors[dst + 1] = baseColor.Y * shade;
            colors[dst + 2] = baseColor.Z * shade;
            colors[dst + 3] = 1.0f;
        }

        return colors;
    }

    /// <summary>
    /// Builds a shaded collision color buffer, using the collision primitive's Attribute flags
    /// as a semantic palette key. The mesh builder keeps polygon vertices separate so colors and
    /// selection highlights cannot leak into neighboring primitives.
    /// </summary>
    public static float[] BuildCollisionVertexColors(
        float[] positions,
        uint[] indices,
        IReadOnlyList<int> trianglePrimitiveIndices,
        IReadOnlyList<ulong> primitiveAttributes)
    {
        int vertexCount = positions.Length / 3;
        if (vertexCount == 0 || indices.Length == 0)
        {
            return Array.Empty<float>();
        }

        var normals = new Vector3[vertexCount];
        var vertexAttributes = new ulong[vertexCount];
        for (int triangle = 0, index = 0; index + 2 < indices.Length; triangle++, index += 3)
        {
            int i0 = (int)indices[index];
            int i1 = (int)indices[index + 1];
            int i2 = (int)indices[index + 2];
            if (i0 < 0 || i0 >= vertexCount || i1 < 0 || i1 >= vertexCount || i2 < 0 || i2 >= vertexCount)
            {
                continue;
            }

            var normal = Vector3.Cross(
                ReadPosition(positions, i1) - ReadPosition(positions, i0),
                ReadPosition(positions, i2) - ReadPosition(positions, i0));
            if (normal.LengthSquared > 1e-8f)
            {
                normal = normal.Normalized();
            }

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;

            var primitiveIndex = triangle < trianglePrimitiveIndices.Count
                ? trianglePrimitiveIndices[triangle]
                : -1;
            var attributes = primitiveIndex >= 0 && primitiveIndex < primitiveAttributes.Count
                ? primitiveAttributes[primitiveIndex]
                : 0;
            vertexAttributes[i0] = attributes;
            vertexAttributes[i1] = attributes;
            vertexAttributes[i2] = attributes;
        }

        var lightDirection = new Vector3(0.35f, 0.75f, 0.55f).Normalized();
        var colors = new float[vertexCount * 4];
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            var normal = normals[vertex];
            if (normal.LengthSquared > 1e-8f)
            {
                normal = normal.Normalized();
            }

            // Collision is useful from both sides, so keep reversed faces just as readable.
            var light = MathF.Abs(Vector3.Dot(normal, lightDirection));
            var shade = 0.58f + 0.42f * light;
            var baseColor = GetCollisionAttributeColor(vertexAttributes[vertex]);
            var destination = vertex * 4;
            colors[destination] = baseColor.X * shade;
            colors[destination + 1] = baseColor.Y * shade;
            colors[destination + 2] = baseColor.Z * shade;
            colors[destination + 3] = 1.0f;
        }

        return colors;
    }

    public static Vector3 GetCollisionAttributeColor(ulong attributes)
    {
        foreach (var style in CollisionAttributeStyles)
        {
            if ((attributes & style.Mask) != 0)
            {
                return style.Color;
            }
        }

        return DefaultCollisionColor;
    }

    public static CollisionTriangleFilterResult FilterCollisionTriangles(
        IReadOnlyList<uint> indices,
        IReadOnlyList<int> trianglePrimitiveIndices,
        IReadOnlyList<int> trianglePolygonIndices,
        IReadOnlyList<ulong> primitiveAttributes,
        ulong? requiredFlag)
    {
        var filteredIndices = new List<uint>(indices.Count);
        var filteredPrimitiveIndices = new List<int>(trianglePrimitiveIndices.Count);
        var filteredPolygonIndices = new List<int>(trianglePolygonIndices.Count);
        var triangleCount = Math.Min(
            indices.Count / 3,
            Math.Min(trianglePrimitiveIndices.Count, trianglePolygonIndices.Count));

        for (var triangle = 0; triangle < triangleCount; triangle++)
        {
            var primitiveIndex = trianglePrimitiveIndices[triangle];
            var attributes = primitiveIndex >= 0 && primitiveIndex < primitiveAttributes.Count
                ? primitiveAttributes[primitiveIndex]
                : 0;
            if (!GeoCollisionAttributes.MatchesFilter(attributes, requiredFlag))
            {
                continue;
            }

            var source = triangle * 3;
            filteredIndices.Add(indices[source]);
            filteredIndices.Add(indices[source + 1]);
            filteredIndices.Add(indices[source + 2]);
            filteredPrimitiveIndices.Add(primitiveIndex);
            filteredPolygonIndices.Add(trianglePolygonIndices[triangle]);
        }

        return new CollisionTriangleFilterResult(
            filteredIndices.ToArray(),
            filteredPrimitiveIndices.ToArray(),
            filteredPolygonIndices.ToArray());
    }

    private static float GetAverageStep(IEnumerable<GeoGroup> groups, int axis)
    {
        float sum = 0f;
        int count = 0;
        foreach (var group in groups)
        {
            float value = axis switch
            {
                0 => group.DivX,
                1 => group.DivY,
                _ => group.DivZ
            };
            if (value > 0.001f)
            {
                sum += value;
                count++;
            }
        }

        return count == 0 ? 0f : sum / count;
    }

    private static float AdjustStep(float min, float max, float step)
    {
        float range = max - min;
        if (range <= 0.01f || step <= 0.01f)
        {
            return step;
        }

        int lines = (int)MathF.Floor(range / step) + 1;
        if (lines <= GridMaxLinesPerAxis)
        {
            return step;
        }

        return range / GridMaxLinesPerAxis;
    }

    private static Vector3 ReadPosition(float[] positions, int index)
    {
        int offset = index * 3;
        return new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
    }
}
