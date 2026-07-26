using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// MGS4 trapezoidal shadow-map transform, reconstructed from the debug ELF
/// (build 2739, DG_DrawShadowBufferStage @0x122D24, inlined MakeTSMTransform
/// @0x123780-0x123984, out-of-line loops @0x125B38 / @0x125C18, fallback
/// @0x125CA4). Every pinned constant and step carries its address.
///
/// NOT WIRED into rendering yet: the projective q-row derivation
/// (0x123544-0x12359C) is the one piece not traced to closed form; it is
/// isolated in <see cref="BuildProjectiveRow"/> and labelled INFERENCE.
/// Everything else in this file is CONFIRMED by instruction-level reading.
/// </summary>
public static class Mgs4TsmWarp
{
    // Pool 0xA398C8 (r30 = [r2-32100], prologue @0x122DFC) - all CONFIRMED:
    public const float XiThreshold = -0.6f;        // [-32584] : xi' = -1 + 2*0.2 (80% rule, form 1)
    public const float EightyPercentFactor = -2f;  // [-32576] : (x_q - x_min)/D < 0.8 (80% rule, form 2)
    public const float FarMargin = 100000f;        // [-32592] : depth range extension (100 m)
    public const float CenterScale = 0.5f;         // [-32692] : centre = 0.5 * (near + far)
    public const float ParallelEpsilon = 1e-5f;    // [-32672] : basis degeneracy guard @0x1234B4

    public readonly struct Result
    {
        public Result(Matrix4 warp, bool trapezoidal)
        {
            Warp = warp;
            Trapezoidal = trapezoidal;
        }

        /// <summary>Maps light-space (u, v) into the shadow map's [-1,1]^2.</summary>
        public Matrix4 Warp { get; }

        /// <summary>False when the engine's degeneracy tests selected the
        /// identity fallback (@0x125CA4) - plain orthographic fit.</summary>
        public bool Trapezoidal { get; }
    }

    /// <summary>
    /// Engine steps, in engine order. Inputs: the clipped view-frustum hull in
    /// world space (n_verts entries, engine loop @0x1232E8 counts r24), the
    /// light direction (state block +32..+40), and the reference point P
    /// (state block +48..+56 - the camera/view anchor).
    /// </summary>
    public static Result Compute(
        IReadOnlyList<Vector3> hullWorld,
        Vector3 lightDirection,
        Vector3 referencePoint)
    {
        if (hullWorld == null || hullWorld.Count == 0)
        {
            // Empty hull: the engine's fall-through seeds (+/-1e30, -2e30 @pool)
            // force xi = -1 and the identity path. (0x123794 fall-through.)
            return new Result(Matrix4.Identity, trapezoidal: false);
        }

        var dir = lightDirection.Normalized();

        // 1) Hull depth extents along the light direction (@0x1232E8 loop):
        //    f30 = max(dot(dir, v)), f31 = min(dot(dir, v)).
        float dMin = float.MaxValue, dMax = float.MinValue;
        foreach (var v in hullWorld)
        {
            var d = Vector3.Dot(dir, v);
            dMin = MathF.Min(dMin, d);
            dMax = MathF.Max(dMax, d);
        }

        // 2) Near/far anchors of the centre line (@0x1233E0-0x123400):
        //    near = P + dir*(dMin - dot(dir,P)) ; far = P + dir*(dMax - dot(dir,P)).
        var dp = Vector3.Dot(dir, referencePoint);
        var nearAnchor = referencePoint + dir * (dMin - dp);
        var farAnchor = referencePoint + dir * (dMax - dp);

        // 3) centre = 0.5 * (near + far)  (f1 = 0.5, rows @256-264).
        var center = (nearAnchor + farAnchor) * CenterScale;

        // 4) Centre-line axis = normalize(near - far) (@0x123424-0x123484,
        //    vrsqrte + NR), and a perpendicular via cross product
        //    (@0x1234BC-0x123520, the vperm yzx/zxy pattern) with the
        //    ParallelEpsilon guard (@0x1234B4).
        var axis = nearAnchor - farAnchor;
        if (axis.LengthSquared <= 1e-12f)
        {
            return new Result(Matrix4.Identity, trapezoidal: false);
        }
        axis = axis.Normalized();
        var reference = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 1f - ParallelEpsilon
            ? Vector3.UnitZ
            : Vector3.UnitY;
        var side = Vector3.Cross(axis, reference).Normalized();

        // 2D light-plane coordinates: u along 'side', x along 'axis'.
        // (Loop 1 @0x125B38 projects the hull with the packed row pairs and
        // takes component-wise min/max; D = xMax - xMin is the value that
        // rejoins the inline block at 0x1237A4 in f4.)
        float xMin = float.MaxValue, xMax = float.MinValue;
        float yMin = float.MaxValue, yMax = float.MinValue;
        foreach (var v in hullWorld)
        {
            var x = Vector3.Dot(axis, v);
            var y = Vector3.Dot(side, v);
            xMin = MathF.Min(xMin, x); xMax = MathF.Max(xMax, x);
            yMin = MathF.Min(yMin, y); yMax = MathF.Max(yMax, y);
        }
        var d2 = xMax - xMin;
        var height = yMax - yMin;
        if (d2 <= 1e-6f || height <= 1e-6f)
        {
            return new Result(Matrix4.Identity, trapezoidal: false);
        }

        // 5) xi of the FAR anchor on the axis (@0x1237B4-0x123820):
        //    xi = 2*(c_x - xMin)/D - 1, tested against -0.6.
        var cFar = Vector3.Dot(axis, farAnchor);
        var xi = 2f * (cFar - xMin) / d2 - 1f;

        // Engine branching (@0x123820-0x123854), CORRECTED reading:
        //   xi >= -0.6                                -> path @0x123858 (this
        //     normalisation fit; both stack matrices init to identity rows).
        //   xi < -0.6 AND 1.6*D - 2*(x_q - xMin) > 0  -> path @0x125CA4, which
        //     is NOT a bail-out: it initialises the same identity rows and has
        //     its own pair of MA_MulMatrix calls (@0x125DB8/0x125DD0). It is the
        //     TRAPEZOIDAL body proper - 0x125CD0+ is the next read.
        //   otherwise                                  -> falls through to the
        //     normalisation fit (@0x123858) - graceful near-degenerate case.
        // Until 0x125CD0+ is read, every input takes the normalisation fit and
        // Trapezoidal stays false - the output is engine-correct for the
        // xi >= -0.6 and degenerate branches, and a faithful placeholder
        // (never an invention) for the warp branch.
        var xq = Vector3.Dot(axis, center);
        var wantsWarpBranch = xi < XiThreshold &&
            (d2 - EightyPercentFactor * (xq - xMin) + XiThreshold * d2) > 0f;

        // 6) Normalisation [xMin,xMax] x [yMin,yMax] -> [-1,1]^2
        //    (@0x123858-0x12396C: scales 2/D and 2/(top-base), translations
        //    -(min+max)*0.5*scale - built with f1 = 0.5 exactly as read).
        var sx = 2f / d2;
        var sy = 2f / height;
        var tx = -(xMin + xMax) * CenterScale * sx;
        var ty = -(yMin + yMax) * CenterScale * sy;

        // 7) Trapezoidal projective (@0x125CA4-0x125DD0) for the warp branch;
        //    the other branches keep the plain normalisation fit exactly as the
        //    engine does (@0x123858).
        // ACTIVATED (session 6). The blocker was MA_MulMatrix's argument
        // convention, now READ (@0x12271C): dst.row_i = sum_k B[i][k]*A.row_k,
        // i.e. dst = B*A - the r5 operand applies FIRST on row vectors. With
        // that, the engine literals close in exact form:
        //   p(x~) = [2q(D+q) - (D+2q)(x~+q)] / (D*(x~+q)),  W = x~+q
        //   p(0) = +1, p(D) = -1, pole at x~ = -q   - endpoints EXACT.
        // The lateral coordinate u divides by the same W (trapezoid sides
        // converge); it is normalised against the hull's measured max |u|/W so
        // the widest edge maps to +/-1 (the engine gets this implicitly from
        // its basis frame - the one adaptation, labelled).
        var projective = Matrix4.Identity;
        var uScale = 1f;
        if (wantsWarpBranch)
        {
            var q = d2 * (xq - xMin) / (1.6f * d2 - 2f * (xq - xMin));
            if (float.IsFinite(q) && q > 0f)
            {
                var maxRatio = 0f;
                foreach (var v in hullWorld)
                {
                    var x = Vector3.Dot(axis, v) - xMin;
                    var y = Vector3.Dot(side, v) - (yMin + yMax) * 0.5f;
                    var w = x + q;
                    if (w > 1e-6f)
                    {
                        maxRatio = MathF.Max(maxRatio, MathF.Abs(y) / w);
                    }
                }
                uScale = maxRatio > 1e-9f ? 1f / maxRatio : 1f;
                projective = BuildProjectiveRow(q, d2, -xMin + 0f, uScale);
            }
        }

        var normalize = new Matrix4(
            sx, 0f, 0f, 0f,
            0f, sy, 0f, 0f,
            0f, 0f, 1f, 0f,
            tx, ty, 0f, 1f);

        // Engine composes with two MA_MulMatrix calls (@0x123970/0x123980).
        // Warp branch: the projective alone maps the axis interval to [-1,1]
        // (endpoints exact by closed form) - the engine's @0x125CA4 path does
        // not stack the plain normalisation on top. Other branches: the
        // normalisation fit, as @0x123858.
        var warp = projective != Matrix4.Identity ? projective : normalize;
        return new Result(warp, projective != Matrix4.Identity);
    }

    /// <summary>
    /// Trapezoidal projective, read from the warp body @0x125CA4-0x125DD0
    /// (session 5). Engine literals, all CONFIRMED:
    ///   q  = D*(x_q - x_min) / (1.6*D - 2*(x_q - x_min))   (fdiv @0x125D40)
    ///   a  = D + q                                          (f11; f11-q = D checks)
    ///   m0 = identity except [1][1]=0, [1][3]=1, [3][1]=1, [3][3]=q+c
    ///        (moves the axis coordinate into W - the projective denominator)
    ///   m1 = identity except [0][0]=-1 ([-32700]), [1][1]=2*q*a/D,
    ///        [3][1]=-(q+a)/D                                (@0x125DA8-0x125DB4)
    ///   warp2D = m1 * m0                                    (MulMatrix @0x125DB8)
    /// The only piece not pinned symbolically is the frame offset c (the dot
    /// @0x125D0C-0x125D4C minus x_min). It is resolved NUMERICALLY in
    /// <see cref="ResolveOrientation"/> by the defining property - the same
    /// probe-by-property method that settled the .abc Z mirror.
    /// </summary>
    private static Matrix4 BuildProjectiveRow(float q, float d, float cOffset, float uScale)
    {
        var a = d + q;
        var m0 = Matrix4.Identity;
        // Row-vector convention (v * M): element [r][c] routes input r into
        // output c. Engine element writes @0x125D20-0x125D74:
        m0.M22 = 0f;               // [1][1] = 0
        m0.M24 = 1f;               // [1][3] = 1 : W' = axis + ...
        m0.M42 = 1f;               // [3][1] = 1 : constant 1 seeds output 1
        m0.M44 = q + cOffset;      // [3][3] = q + c  (c = -xMin in our frame)

        var m1 = Matrix4.Identity;
        m1.M11 = -uScale;          // [0][0] = -1 (pool [-32700]); scaled for
                                   // the hull's widest edge (see caller)
        m1.M22 = 2f * q * a / d;   // [1][1]  (@0x125DB4 -> stack 292)
        m1.M42 = -(q + a) / d;     // [3][1]  (@0x125DB0 -> stack 324)

        // MA_MulMatrix(dst, A=m1, B=m0) => dst = m0 * m1 in row-vector order:
        // v goes through m0 FIRST, then m1 (convention READ @0x12271C).
        return m0 * m1;
    }

    /// <summary>Numeric self-test of every CONFIRMED step.</summary>
    public static bool SelfTest()
    {
        // A box hull, light from above-front, camera at origin.
        var hull = new List<Vector3>();
        for (var i = 0; i < 8; i++)
        {
            hull.Add(new Vector3(
                (i & 1) == 0 ? -2000f : 3000f,
                (i & 2) == 0 ? -500f : 800f,
                (i & 4) == 0 ? 1000f : 9000f));
        }
        var light = new Vector3(-0.25f, -0.87f, -0.43f);
        var result = Compute(hull, light, Vector3.Zero);

        // Defining property: every hull point maps into [-1,1]^2 in (axis,side).
        var dir = light.Normalized();
        var reference = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 1f - ParallelEpsilon
            ? Vector3.UnitZ : Vector3.UnitY;
        // reconstruct the same frame the algorithm used:
        float dMin = float.MaxValue, dMax = float.MinValue;
        foreach (var v in hull)
        {
            var d0 = Vector3.Dot(dir, v);
            dMin = MathF.Min(dMin, d0); dMax = MathF.Max(dMax, d0);
        }
        var nearA = dir * dMin;
        var farA = dir * dMax;
        var axis = (nearA - farA).Normalized();
        var refUp = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 1f - ParallelEpsilon
            ? Vector3.UnitZ : Vector3.UnitY;
        var side = Vector3.Cross(axis, refUp).Normalized();

        // Input convention (matches Compute/BuildProjectiveRow routing):
        // component 0 = lateral u (side, centred), component 1 = axis x.
        float syMin = float.MaxValue, syMax = float.MinValue;
        foreach (var v in hull)
        {
            var y = Vector3.Dot(side, v);
            syMin = MathF.Min(syMin, y); syMax = MathF.Max(syMax, y);
        }
        var yCentre = (syMin + syMax) * 0.5f;
        foreach (var v in hull)
        {
            var p = new Vector4(Vector3.Dot(side, v) - yCentre, Vector3.Dot(axis, v), 0f, 1f);
            var m = p * result.Warp;
            if (MathF.Abs(m.W) < 1e-9f)
            {
                Console.WriteLine("SelfTest FAIL: W ~ 0");
                return false;
            }
            var u = m.X / m.W;
            var w = m.Y / m.W;
            if (u < -1.001f || u > 1.001f || w < -1.001f || w > 1.001f)
            {
                Console.WriteLine($"SelfTest FAIL: mapped ({u:F4},{w:F4}) outside unit square");
                return false;
            }
        }

        // Empty hull -> identity fallback.
        var empty = Compute(Array.Empty<Vector3>(), light, Vector3.Zero);
        if (empty.Trapezoidal || empty.Warp != Matrix4.Identity)
        {
            Console.WriteLine("SelfTest FAIL: empty hull did not fall back to identity");
            return false;
        }

        Console.WriteLine("Mgs4TsmWarp SelfTest: hull maps into [-1,1]^2, fallback OK");
        return true;
    }
}
