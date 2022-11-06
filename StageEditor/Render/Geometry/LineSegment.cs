using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a line segment in 3D space
    /// </summary>
    public class LineSegment: Line
    {
        /// <summary>
        /// Gets or sets the end point of this line segment.
        /// </summary>
        public Vector3d EndPoint { get; set; }

        /// <summary>
        /// Gets the center point of this line segment.
        /// </summary>
        public Vector3d Center
        {
            get
            {
                return (Point + EndPoint) / 2;
            }
        }

        /// <summary>
        /// Gets the length of this line segment.
        /// </summary>
        public double Length
        {
            get
            {
                return Math.Sqrt(this.LengthSquared);
            }
        }

        /// <summary>
        /// Gets the squared length of this line segment.
        /// </summary>
        public double LengthSquared
        {
            get
            {
                return (EndPoint - Point).LengthSquared;
            }
        }

        /// <summary>
        /// Initializes a new line segments using the provided end points.
        /// </summary>
        /// <param name="point1">The first endpoint.</param>
        /// <param name="point2">The second endpoint.</param>
        public LineSegment(Vector3d point1, Vector3d point2) : base(point1, point2, false)
        {
            this.EndPoint = point2;
        }

        /// <summary>
        /// Performs intersection test between this line segment and the specified line.
        /// Test adapted from https://www.codefull.org/2015/06/intersection-of-a-ray-and-a-line-segment-in-3d/
        /// </summary>
        /// <param name="line">The line</param>
        /// <returns>If intersection occurs, the point of intersection. null otherwise.</returns>
        public Vector3d? IntersectionWith(Line line)
        {
            var da = line.Direction;
            var db = this.EndPoint - this.Point;
            var dc = this.Point - line.Point;

            if (Math.Abs(Vector3d.Dot(dc, Vector3d.Cross(da, db))) >= 0.7) // lines are not coplanar
                return null;

            var s = Vector3d.Dot(Vector3d.Cross(dc, db), Vector3d.Cross(da, db)) / (Vector3d.Cross(da, db).LengthSquared);
            var ip = line.Point + da * s;
            ip = new Vector3d(Math.Round(ip.X, 3), Math.Round(ip.Y, 3), Math.Round(ip.Z, 3));

            // Is the intersection point between the previous point and the current point?
            if ((ip - Point).LengthSquared + (EndPoint - ip).LengthSquared <= LengthSquared + 1e-3)
                return ip;

            return null;
        }

        /// <summary>
        /// Performs intersection test between this line segment and the specified ray.
        /// Test adapted from https://www.codefull.org/2015/06/intersection-of-a-ray-and-a-line-segment-in-3d/
        /// </summary>
        /// <param name="ray">The ray</param>
        /// <returns>If intersection occurs, the point of intersection. null otherwise.</returns>
        public Vector3d? IntersectionWith(Ray ray)
        {
            var da = ray.EndPoint - ray.Origin;
            var db = EndPoint - Point;
            var dc = Point - ray.Origin;

            if (Math.Abs(Vector3d.Dot(dc, Vector3d.Cross(da, db))) >= 0.7) // lines are not coplanar
                return null;

            var s = Vector3d.Dot(Vector3d.Cross(dc, db), Vector3d.Cross(da, db)) / (Vector3d.Cross(da, db).LengthSquared);
            if (s >= 0.0 && s <= 1.0)
            {
                var ip = ray.Origin + da * s;
                ip = new Vector3d(Math.Round(ip.X, 3), Math.Round(ip.Y, 3), Math.Round(ip.Z, 3));

                // Is the intersection point between the previous point and the current point?
                if ((ip - Point).LengthSquared + (EndPoint - ip).LengthSquared <= LengthSquared + 1e-3)
                    return ip;
            }

            return null;
        }

        /// <summary>
        /// Performs intersection test between this line segment and the provided point.
        /// In other words, determines whether the poin lies on this line segment or not.
        /// </summary>
        /// <param name="point">The point</param>
        /// <returns>If intersection occurs, the point of intersection. null otherwise.</returns>
        public Vector3d? IntersectionWith(Vector3d point)
        {
            var d1 = point.DistanceSquared(this.Point);
            var d2 = point.DistanceSquared(this.EndPoint);

            if (Math.Abs((d1 + d2) - this.LengthSquared) < 0.0002)  // Difference should be less than a threshold
                return point;

            return null;
        }
    }
}
