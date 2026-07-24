using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// Exact spatial ambient-cube evaluation, reconstructed instruction-by-
/// instruction from the MGS4 debug ELF build 2739 (ambcube.cc):
///   GetAmbient            @ 0x127EBC  (recursive tree partition of unity)
///   DG_GetAmbientCube     @ 0x128520  (32 region slots + global fallback)
///
/// Constants pinned from the constant pool (cpool 0xA399C4, TOC 0xA90D58):
///   denominator floor {0.1,0.1,0.1}, region band {1000,1000,1000},
///   epsilon 1e-5f, scalars 0.0f / 1.0f.
///
/// Validated against ambcube_full_ref.c (gcc): 5 GetAmbient tests +
/// 5 wrapper tests, all passing. <see cref="SelfTest"/> transcribes them.
///
/// Notes carried from the disassembly:
///  - scene_num is IGNORED by build 2739's DG_GetAmbientCube.
///  - Roots of a slot start at node[0] and are chained by SiblingIdx.
///  - If neither slots nor the global slot contribute, the engine leaves the
///    output UNWRITTEN; here TryEvaluate returns false instead.
///  - Face index order 0..5 is mapped to Left,Right,Top,Bottom,Front,Back per
///    the NewSystemLightSet slot naming already established in Haven
///    (HIGH-CONFIDENCE; to be re-verified against the preshader consumer).
///
/// This evaluator is INACTIVE until region data is registered via
/// <see cref="SetSlots"/> — Haven's rendering is unchanged by default.
/// The AMBCUBE data source (GCX NewRenderAmbcube / LT3 mapping) is the next
/// reverse-engineering work item and is intentionally not guessed here.
/// </summary>
public static class Mgs4AmbientCubeEvaluator
{
    public const float DenomFloor = 0.1f;
    public const float RegionBand = 1000.0f;
    public const float Epsilon = 1e-5f;

    /// <summary>_AMBCUBE, 192 bytes in the engine (DWARF-confirmed layout).</summary>
    public sealed class Node
    {
        public Vector3 InMin, InMax;      // 0x00, 0x10
        public Vector3 OutMin, OutMax;    // 0x20, 0x30
        public Vector3 Center;            // 0x40 (not read by the evaluator)
        public Vector3[] Ambient = new Vector3[6]; // 0x50
        public int ParentIdx = -1;        // 0xB0 (not read by the evaluator)
        public int ChildIdx = -1;         // 0xB4
        public int SiblingIdx = -1;       // 0xB8
    }

    /// <summary>Registered region: 48-byte header {.., regionMin@0x10, regionMax@0x20}
    /// followed by the node array; roots begin at Nodes[0].</summary>
    public sealed class Slot
    {
        public Vector3 RegionMin, RegionMax;
        public Node[] Nodes = Array.Empty<Node>();
    }

    private static Slot?[] _slots = new Slot?[32];
    private static Slot? _global;

    public static void SetSlots(IReadOnlyList<Slot?>? slots, Slot? global)
    {
        var next = new Slot?[32];
        if (slots != null)
        {
            for (int i = 0; i < next.Length && i < slots.Count; i++)
            {
                next[i] = slots[i];
            }
        }
        _slots = next;
        _global = global;
    }

    public static void Clear() => SetSlots(null, null);

    public static bool HasData
    {
        get
        {
            if (_global != null) return true;
            foreach (var s in _slots) if (s != null) return true;
            return false;
        }
    }

    /// <summary>
    /// DG_GetAmbientCube. Returns false when nothing contributes (the engine
    /// would leave the destination unwritten in that case).
    /// </summary>
    private static int _diagBudget = 5;

    public static bool TryEvaluate(Vector3 position, out AmbientCubeLighting cube)
    {
        cube = default;
        if (!HasData)
        {
            if (_diagBudget > 0)
            {
                _diagBudget--;
                Mgs4Diagnostics.Log("CUBE", "TryEvaluate: no registered data");
            }
            return false;
        }

        var diag = _diagBudget > 0;
        if (diag)
        {
            _diagBudget--;
            Mgs4Diagnostics.Log("CUBE",
                $"TryEvaluate at ({position.X:F0}, {position.Y:F0}, {position.Z:F0})");
            for (var i = 0; i < _slots.Length; i++)
            {
                var diagSlot = _slots[i];
                if (diagSlot == null) continue;
                var rmin = diagSlot.RegionMin;
                var rmax = diagSlot.RegionMax;
                var inside =
                    position.X >= rmin.X && position.X <= rmax.X &&
                    position.Y >= rmin.Y && position.Y <= rmax.Y &&
                    position.Z >= rmin.Z && position.Z <= rmax.Z;
                Mgs4Diagnostics.Log("CUBE",
                    $"  slot[{i}] region ({rmin.X:F0},{rmin.Y:F0},{rmin.Z:F0})" +
                    $"..({rmax.X:F0},{rmax.Y:F0},{rmax.Z:F0}) -> " +
                    (inside ? "INSIDE" : "OUTSIDE"));
                if (!inside)
                {
                    // Report which axes fail, and whether a mirrored axis would
                    // land inside. A world-space convention difference between the
                    // viewer and the MGS4 data shows up here immediately.
                    var axes = "";
                    if (position.X < rmin.X || position.X > rmax.X) axes += "X ";
                    if (position.Y < rmin.Y || position.Y > rmax.Y) axes += "Y ";
                    if (position.Z < rmin.Z || position.Z > rmax.Z) axes += "Z ";
                    var mz = -position.Z;
                    var mirroredInside =
                        position.X >= rmin.X && position.X <= rmax.X &&
                        position.Y >= rmin.Y && position.Y <= rmax.Y &&
                        mz >= rmin.Z && mz <= rmax.Z;
                    Mgs4Diagnostics.Log("CUBE",
                        $"    failing axes: {axes.Trim()} | with Z negated -> " +
                        (mirroredInside ? "INSIDE (axis convention mismatch)" : "still outside"));
                }
            }
        }

        Span<Vector3> acc = stackalloc Vector3[6];
        Span<Vector3> s = stackalloc Vector3[6];
        float sigma = 0f;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;
            float f27 = EvalSlot(s, slot, position);
            if (f27 <= 0f) continue;
            for (int k = 0; k < 6; k++) acc[k] += s[k] * f27;
            sigma += f27;
        }

        bool wrote = false;
        Span<Vector3> outFaces = stackalloc Vector3[6];
        if (sigma > Epsilon)                        // 0x128C74: out = Acc/Sigma
        {
            float inv = 1f / sigma;
            for (int k = 0; k < 6; k++) outFaces[k] = acc[k] * inv;
            wrote = true;
        }
        if (_global != null && sigma < 1.0f)        // 0x128D8C
        {
            float f27g = EvalSlot(s, _global, position);
            if (f27g > Epsilon)                     // 0x1291DC
            {
                // out = (Acc/Sigma)*Sigma + G*(1-Sigma) = Acc + G*(1-Sigma)
                float r = 1f - sigma;
                for (int k = 0; k < 6; k++) outFaces[k] = acc[k] + s[k] * r;
                wrote = true;
            }
        }
        if (!wrote)
        {
            if (diag)
            {
                Mgs4Diagnostics.Log("CUBE", "  result: NO contribution (falling back to flat ambient)");
            }
            return false;
        }

        cube = new AmbientCubeLighting(
            outFaces[0], outFaces[1], outFaces[2],
            outFaces[3], outFaces[4], outFaces[5]);
        if (diag)
        {
            Mgs4Diagnostics.Log("CUBE",
                $"  result: HIT sigma-weighted faces L={cube.Left} R={cube.Right} T={cube.Top}");
        }
        return true;
    }

    /// <summary>Slot evaluation shared by the 32 slots and the global slot.
    /// Writes the normalised mixture into <paramref name="s"/> and returns
    /// f27 = band * min(sumW, 1); 0 when the slot does not contribute.</summary>
    private static float EvalSlot(Span<Vector3> s, Slot slot, Vector3 pos)
    {
        for (int k = 0; k < 6; k++) s[k] = Vector3.Zero;

        // interior test, boundary inclusive (0x1286CC / 0x128DF4)
        float inD = MathF.Min(
            Min3(pos.X - slot.RegionMin.X, pos.Y - slot.RegionMin.Y, pos.Z - slot.RegionMin.Z),
            Min3(slot.RegionMax.X - pos.X, slot.RegionMax.Y - pos.Y, slot.RegionMax.Z - pos.Z));
        if (inD < 0f) return 0f;

        // fixed-width region band, 1000 per axis (0x128838 / 0x128E40)
        float band = Clamp01(MathF.Min(
            Min3((pos.X - slot.RegionMin.X) / RegionBand,
                 (pos.Y - slot.RegionMin.Y) / RegionBand,
                 (pos.Z - slot.RegionMin.Z) / RegionBand),
            Min3((slot.RegionMax.X - pos.X) / RegionBand,
                 (slot.RegionMax.Y - pos.Y) / RegionBand,
                 (slot.RegionMax.Z - pos.Z) / RegionBand)));
        if (band <= 0f) return 0f;
        if (slot.Nodes.Length == 0) return 0f;

        Span<Vector3> t = stackalloc Vector3[6];
        float sw = 0f;
        for (int idx = 0; idx >= 0; idx = slot.Nodes[idx].SiblingIdx)
        {
            var c = slot.Nodes[idx];
            float w = NodeWeight(c, pos);
            if (w > 0f)
            {
                GetAmbient(t, c, slot.Nodes, pos);
                for (int k = 0; k < 6; k++) s[k] += t[k] * w;
                sw += w;
            }
        }
        if (sw <= Epsilon) return 0f;
        float inv = 1f / sw;
        for (int k = 0; k < 6; k++) s[k] *= inv;
        return band * MathF.Min(sw, 1f);
    }

    /// <summary>GetAmbient @0x127EBC — recursive partition of unity.</summary>
    public static void GetAmbient(Span<Vector3> outFaces, Node node,
                                  Node[] baseNodes, Vector3 pos)
    {
        for (int k = 0; k < 6; k++) outFaces[k] = Vector3.Zero;
        float sumW = 0f;

        if (node.ChildIdx >= 0)
        {
            Span<Vector3> t = stackalloc Vector3[6];
            for (int idx = node.ChildIdx; idx >= 0; idx = baseNodes[idx].SiblingIdx)
            {
                var c = baseNodes[idx];
                float w = NodeWeight(c, pos);
                if (w > 0f)
                {
                    GetAmbient(t, c, baseNodes, pos);
                    for (int k = 0; k < 6; k++) outFaces[k] += t[k] * w;
                    sumW += w;
                }
            }
        }

        if (node.ChildIdx < 0 || sumW <= 0f)
        {
            for (int k = 0; k < 6; k++) outFaces[k] = node.Ambient[k]; // 0x128330
        }
        else if (sumW >= 1f)
        {
            float inv = 1f / sumW;                                     // 0x128204
            for (int k = 0; k < 6; k++) outFaces[k] *= inv;
        }
        else
        {
            float r = 1f - sumW;                                       // 0x1283C8
            for (int k = 0; k < 6; k++) outFaces[k] += node.Ambient[k] * r;
        }
    }

    /// <summary>clamp01(min(tMax,tMin)) with 0.1-floored denominators —
    /// the per-node ramp used both inside GetAmbient and for slot roots.</summary>
    private static float NodeWeight(Node c, Vector3 pos)
    {
        float tMax = Min3(
            (c.OutMax.X - pos.X) / MathF.Max(c.OutMax.X - c.InMax.X, DenomFloor),
            (c.OutMax.Y - pos.Y) / MathF.Max(c.OutMax.Y - c.InMax.Y, DenomFloor),
            (c.OutMax.Z - pos.Z) / MathF.Max(c.OutMax.Z - c.InMax.Z, DenomFloor));
        float tMin = Min3(
            (pos.X - c.OutMin.X) / MathF.Max(c.InMin.X - c.OutMin.X, DenomFloor),
            (pos.Y - c.OutMin.Y) / MathF.Max(c.InMin.Y - c.OutMin.Y, DenomFloor),
            (pos.Z - c.OutMin.Z) / MathF.Max(c.InMin.Z - c.OutMin.Z, DenomFloor));
        return Clamp01(MathF.Min(tMax, tMin));
    }

    private static float Min3(float a, float b, float c) =>
        MathF.Min(a, MathF.Min(b, c));

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    /// <summary>Transcription of the ten reference tests (ambcube_ref.c +
    /// ambcube_full_ref.c). Returns true when all pass. Callable from a
    /// debug menu; leaves registered data untouched.</summary>
    public static bool SelfTest()
    {
        static Node N() => new Node();
        static void Faces(Node n, float v)
        { for (int k = 0; k < 6; k++) n.Ambient[k] = new Vector3(v, v, v); }
        static bool Near(float a, float b)
        { float m = MathF.Max(MathF.Abs(b), 1f); return MathF.Abs(a - b) < 1e-3f * m; }

        // tree: root(1.0) -> node1(10, in±1 out±3) -> node2(100, in±0.5 out±1)
        var n0 = N(); n0.ChildIdx = 1; Faces(n0, 1f);
        var n1 = N();
        n1.InMin = new Vector3(-1); n1.InMax = new Vector3(1);
        n1.OutMin = new Vector3(-3); n1.OutMax = new Vector3(3);
        n1.ParentIdx = 0; n1.ChildIdx = 2; Faces(n1, 10f);
        var n2 = N();
        n2.InMin = new Vector3(-0.5f); n2.InMax = new Vector3(0.5f);
        n2.OutMin = new Vector3(-1); n2.OutMax = new Vector3(1);
        n2.ParentIdx = 1; Faces(n2, 100f);
        var nodes = new[] { n0, n1, n2 };

        Span<Vector3> o = stackalloc Vector3[6];
        bool ok = true;
        GetAmbient(o, n0, nodes, new Vector3(0, 0, 0));      ok &= Near(o[0].X, 100f);
        GetAmbient(o, n0, nodes, new Vector3(0, 0, 0.99f));  ok &= Near(o[0].X, 0.98f * 10f + 0.02f * 100f);
        GetAmbient(o, n0, nodes, new Vector3(10, 10, 10));   ok &= Near(o[0].X, 1f);
        GetAmbient(o, n0, nodes, new Vector3(0, 0, 2));      ok &= Near(o[0].X, 5.5f);

        // degenerate band -> behaves as 0.1-wide
        var d0 = N(); d0.ChildIdx = 1; Faces(d0, 1f);
        var d1 = N();
        d1.InMin = d1.OutMin = new Vector3(-1);
        d1.InMax = d1.OutMax = new Vector3(1);
        d1.ParentIdx = 0; Faces(d1, 100f);
        GetAmbient(o, d0, new[] { d0, d1 }, new Vector3(0, 0, 0.95f));
        ok &= Near(o[0].X, 50.5f);

        // wrapper: slot A (region ±3000) with the tree above; global(1.0) ±1e6
        var slotA = new Slot
        {
            RegionMin = new Vector3(-3000), RegionMax = new Vector3(3000),
            Nodes = new[] { CloneRoot(n1, 10f), CloneLeaf(n2) }
        };
        var g0 = N();
        g0.InMin = g0.OutMin = new Vector3(-1e6f);
        g0.InMax = g0.OutMax = new Vector3(1e6f);
        Faces(g0, 1f);
        var global = new Slot
        {
            RegionMin = new Vector3(-1e6f), RegionMax = new Vector3(1e6f),
            Nodes = new[] { g0 }
        };

        var savedSlots = _slots; var savedGlobal = _global;
        try
        {
            var arr = new Slot?[32]; arr[3] = slotA;
            _slots = arr; _global = global;

            ok &= TryEvaluate(new Vector3(0, 0, 0), out var c1) && Near(c1.Left.X, 100f);
            ok &= TryEvaluate(new Vector3(0, 0, 500), out var c2) && Near(c2.Left.X, 1f);
            ok &= TryEvaluate(new Vector3(0, 0, 2), out var c3) && Near(c3.Left.X, 5.5f);
            _global = null;
            ok &= TryEvaluate(new Vector3(0, 0, 2), out var c4) && Near(c4.Left.X, 10f);
        }
        finally
        {
            _slots = savedSlots; _global = savedGlobal;
        }
        return ok;

        static Node CloneRoot(Node src, float faces)
        {
            var r = N();
            r.InMin = src.InMin; r.InMax = src.InMax;
            r.OutMin = src.OutMin; r.OutMax = src.OutMax;
            r.ChildIdx = 1; r.SiblingIdx = -1; Faces(r, faces);
            return r;
        }
        static Node CloneLeaf(Node src)
        {
            var l = N();
            l.InMin = src.InMin; l.InMax = src.InMax;
            l.OutMin = src.OutMin; l.OutMax = src.OutMax;
            l.ParentIdx = 0; l.ChildIdx = -1; l.SiblingIdx = -1;
            for (int k = 0; k < 6; k++) l.Ambient[k] = src.Ambient[k];
            return l;
        }
    }
}
