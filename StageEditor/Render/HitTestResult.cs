using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents the generalized result of a hit test performed on a Drawable. The hit test
    /// can be either performed in 2D or 3D.
    /// </summary>
    public class HitTestResult
    {
        /// <summary>
        /// The drawable that was hit by the ray
        /// </summary>
        public Drawable3D Drawable { get; protected set; }

        /// <summary>
        /// A set of triangle vertices that intersect the ray
        /// </summary>
        public Vector3d HitPoint { get; protected set; }

        /// <summary>
        /// Instantiates a new HitTestResult instance on the specified Drawable with the 
        /// specified hit point.
        /// </summary>
        /// <param name="drawable">The hit Drawable.</param>
        /// <param name="hitPoint">The point of hit.</param>
        public HitTestResult(Drawable3D drawable, Vector3d hitPoint)
        {
            this.Drawable = drawable;
            this.HitPoint = hitPoint;
        }

        /// <summary>
        /// Instantiates a new HitTestResult instance on the specified Drawable with the 
        /// specified hit point.
        /// </summary>
        /// <param name="drawable">The hit Drawable.</param>
        /// <param name="hitPoint">The point of hit.</param>
        public HitTestResult(Drawable3D drawable, Vector2d hitPoint) : this(drawable, new Vector3d(hitPoint)) { }
    }
}
