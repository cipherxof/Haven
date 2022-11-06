using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a result of a hit test between a ray and a triangular mesh.
    /// </summary>
    public class MeshHitTestResult : HitTestResult
    {
        /// <summary>
        /// Gets the triangle that was hit by the specified ray.
        /// </summary>
        public Triangle TriangleHit { get; protected set; }

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="drawable">The Drawable instance that was hit.</param>
        /// <param name="hitPoint">The hit point.</param>
        /// <param name="triangleHit">The triangle that was hit.</param>
        public MeshHitTestResult(Drawable3D drawable, Vector3d hitPoint, Triangle triangleHit) : base(drawable, hitPoint)
        {
            this.TriangleHit = new Triangle(triangleHit.A, triangleHit.B, triangleHit.C);
        }
    }
}
