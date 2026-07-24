using System;
using System.Buffers.Binary;
using System.IO;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Abc;

/// <summary>
/// MGS4 ambient-cube file (".abc"). The file bytes are the in-memory layout
/// consumed by the ambient-cube evaluation:
///
///   0x00  char  magic[4]      "AMBC"
///   0x04  u16   version       (2 observed on sm_dd)
///   0x06  u16   reserved
///   0x08  u32   nodeCount
///   0x0C  u32   pad
///   0x10  FVECTOR regionMin
///   0x20  FVECTOR regionMax
///   0x30  AMBCUBE nodes[nodeCount], 192 bytes each (big-endian):
///         inMin, inMax, outMin, outMax, center : FVECTOR
///         ambient[6]                            : FVECTOR
///         parentIdx, childIdx, siblingIdx, pad  : s32
///
/// Root nodes start at nodes[0], chained by siblingIdx; children via childIdx.
/// The inner box is the region inset by the fixed 1000-unit band per face.
/// The registration id only selects a table entry - it does not affect
/// evaluation.
/// </summary>
public sealed class AbcFile
{
    public const uint Magic = 0x414D4243; // "AMBC"
    public const int HeaderSize = 48;
    public const int NodeSize = 192;

    public ushort Version { get; private init; }
    public Vector3 RegionMin { get; private init; }
    public Vector3 RegionMax { get; private init; }
    public Mgs4AmbientCubeEvaluator.Node[] Nodes { get; private init; } =
        Array.Empty<Mgs4AmbientCubeEvaluator.Node>();

    public static AbcFile Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new InvalidDataException("ABC file truncated: header incomplete.");
        }
        if (BinaryPrimitives.ReadUInt32BigEndian(data) != Magic)
        {
            throw new InvalidDataException("ABC file: bad magic (expected 'AMBC').");
        }

        var version = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        var nodeCount = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var expected = HeaderSize + (long)nodeCount * NodeSize;
        if (nodeCount > int.MaxValue / NodeSize || data.Length < expected)
        {
            throw new InvalidDataException(
                $"ABC file: {nodeCount} node(s) declared but only {data.Length} byte(s) present.");
        }

        var nodes = new Mgs4AmbientCubeEvaluator.Node[nodeCount];
        for (var i = 0; i < nodes.Length; i++)
        {
            var n = data.Slice(HeaderSize + i * NodeSize, NodeSize);
            var node = new Mgs4AmbientCubeEvaluator.Node
            {
                InMin = ReadVector3(n, 0x00),
                InMax = ReadVector3(n, 0x10),
                OutMin = ReadVector3(n, 0x20),
                OutMax = ReadVector3(n, 0x30),
                Center = ReadVector3(n, 0x40),
                ParentIdx = BinaryPrimitives.ReadInt32BigEndian(n[0xB0..]),
                ChildIdx = BinaryPrimitives.ReadInt32BigEndian(n[0xB4..]),
                SiblingIdx = BinaryPrimitives.ReadInt32BigEndian(n[0xB8..]),
            };
            for (var k = 0; k < 6; k++)
            {
                node.Ambient[k] = ReadVector3(n, 0x50 + k * 16);
            }
            ValidateIndex(node.ParentIdx, nodes.Length, i, "parentIdx");
            ValidateIndex(node.ChildIdx, nodes.Length, i, "childIdx");
            ValidateIndex(node.SiblingIdx, nodes.Length, i, "siblingIdx");
            nodes[i] = node;
        }

        return new AbcFile
        {
            Version = version,
            RegionMin = ReadVector3(data, 0x10),
            RegionMax = ReadVector3(data, 0x20),
            Nodes = nodes,
        };
    }

    /// <summary>
    /// Converts the file's boxes into the viewer's world space.
    ///
    /// Measured, not assumed: with the stage loaded, every sample reported
    ///   TryEvaluate at (63750, -500, 119875)
    ///   region (-100000,-11000,-140000)..(153000,18000,-28000) -> OUTSIDE
    ///   failing axes: Z | with Z negated -> INSIDE
    /// X and Y land inside the region on every sample and only Z is mirrored, so
    /// the viewer's world space uses the opposite Z sign from the MGS4 stage data.
    /// The .abc boxes are therefore mirrored once, at load time, instead of
    /// mirroring every query position.
    ///
    /// Negating an interval reverses it, so each Z pair is swapped as well:
    /// [zMin, zMax] becomes [-zMax, -zMin]. The Front/Back ambient faces are
    /// swapped for the same reason — they are the two faces the Z axis addresses.
    /// (Face order L,R,T,B,F,Bk is Haven's existing convention and is still
    /// HIGH-CONFIDENCE rather than proven; if it is ever corrected, this swap
    /// must follow it.)
    /// </summary>
    public Mgs4AmbientCubeEvaluator.Slot ToSlot(bool mirrorZ = true)
    {
        if (!mirrorZ)
        {
            return new Mgs4AmbientCubeEvaluator.Slot
            {
                RegionMin = RegionMin,
                RegionMax = RegionMax,
                Nodes = Nodes,
            };
        }

        var nodes = new Mgs4AmbientCubeEvaluator.Node[Nodes.Length];
        for (var i = 0; i < Nodes.Length; i++)
        {
            var src = Nodes[i];
            var dst = new Mgs4AmbientCubeEvaluator.Node
            {
                InMin = MirrorMin(src.InMin, src.InMax),
                InMax = MirrorMax(src.InMin, src.InMax),
                OutMin = MirrorMin(src.OutMin, src.OutMax),
                OutMax = MirrorMax(src.OutMin, src.OutMax),
                Center = new Vector3(src.Center.X, src.Center.Y, -src.Center.Z),
                ParentIdx = src.ParentIdx,
                ChildIdx = src.ChildIdx,
                SiblingIdx = src.SiblingIdx,
            };
            for (var k = 0; k < 6; k++)
            {
                dst.Ambient[k] = src.Ambient[k];
            }
            (dst.Ambient[4], dst.Ambient[5]) = (src.Ambient[5], src.Ambient[4]);
            nodes[i] = dst;
        }

        return new Mgs4AmbientCubeEvaluator.Slot
        {
            RegionMin = MirrorMin(RegionMin, RegionMax),
            RegionMax = MirrorMax(RegionMin, RegionMax),
            Nodes = nodes,
        };
    }

    private static Vector3 MirrorMin(Vector3 min, Vector3 max) =>
        new(min.X, min.Y, -max.Z);

    private static Vector3 MirrorMax(Vector3 min, Vector3 max) =>
        new(max.X, max.Y, -min.Z);

    private static Vector3 ReadVector3(ReadOnlySpan<byte> data, int offset) => new(
        BinaryPrimitives.ReadSingleBigEndian(data[offset..]),
        BinaryPrimitives.ReadSingleBigEndian(data[(offset + 4)..]),
        BinaryPrimitives.ReadSingleBigEndian(data[(offset + 8)..]));

    private static void ValidateIndex(int value, int count, int node, string field)
    {
        if (value < -1 || value >= count)
        {
            throw new InvalidDataException(
                $"ABC file: node {node} has out-of-range {field} {value} (count {count}).");
        }
    }
}
