using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represens a triangle in 3D space with its 3 vertices
    /// </summary>
    public class Triangle
    {
        /// <summary>
        /// Edge #1 used for ray-triangle intersection tests
        /// </summary>
        protected Vector3d e1;

        /// <summary>
        /// Edge #2 used for ray-triangle intersection tests
        /// </summary>
        protected Vector3d e2;

        /// <summary>
        /// The first vertex of the triangle
        /// </summary>
        protected Vector3d a;

        /// <summary>
        /// Gets or sets the first vertex of the traignle
        /// </summary>
        public Vector3d A
        {
            get { return a; }
            set { a = value; CalculateNormal(); }
        }

        /// <summary>
        /// The second vertex of the triangle
        /// </summary>
        protected Vector3d b;

        /// <summary>
        /// Gets or sets the second vertex of the traignle
        /// </summary>
        public Vector3d B
        {
            get { return b; }
            set { b = value; CalculateNormal(); }
        }

        /// <summary>
        /// The third vertex of the triangle
        /// </summary>
        protected Vector3d c;

        /// <summary>
        /// Gets or sets the third vertex of the traignle
        /// </summary>
        public Vector3d C
        {
            get { return c; }
            set { c = value; CalculateNormal(); }
        }

        /// <summary>
        /// Gets the normal of this triangle. The vector is normalized.
        /// </summary>
        public Vector3d Normal { get; private set; }

        /// <summary>
        /// Converts the information of this triangle into a blitted
        /// double[] array. The sequence is A, B, C, e1, e2, Normal)
        /// </summary>
        /// <returns>A double[] array containing this triangle's information.</returns>
        public double[] ToBlittableArray()
        {
            List<double> k = new List<double>();
            k.AddRange(this.A.ToBlittableArray());
            k.AddRange(this.B.ToBlittableArray());
            k.AddRange(this.C.ToBlittableArray());
            k.AddRange(this.e1.ToBlittableArray());
            k.AddRange(this.e2.ToBlittableArray());
            k.AddRange(this.Normal.ToBlittableArray());

            return k.ToArray();
        }

        /// <summary>
        /// Calculates the normal of this triangle
        /// </summary>
        protected void CalculateNormal()
        {
            this.Normal = Vector3d.Cross(e1, e2).Normalized();
        }

        /// <summary>
        /// Creates a new Triangle instance using the specified vertices.
        /// </summary>
        /// <param name="a">The first vertex of the triangle.</param>
        /// <param name="b">The second vertex of the triangle.</param>
        /// <param name="c">The third vertex of the triangle.</param>
        public Triangle(Vector3d a, Vector3d b, Vector3d c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            // Precompute edges that help the intersection
            this.e1 = B - A;
            this.e2 = C - A;

            this.CalculateNormal();
        }

        /// <summary>
        /// Creates a new Triangle instance with all vertices set to zero.
        /// </summary>
        public Triangle() : this(Vector3d.Zero, Vector3d.Zero, Vector3d.Zero) { }

        /// <summary>
        /// Intersection test between a triangle and a ray based on Moller-Trumbore algorithm
        /// (see: http://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm)
        /// </summary>
        /// <param name="ray">The ray to test for intersections</param>
        /// <returns>The point of intersection (if intersection occurs). null otherwise.</returns>
        public Vector3d? IntersectionWith(Ray ray)
        {
            Vector3d P = Vector3d.Cross(ray.Direction, e2);
            double det = Vector3d.Dot(e1, P);

            if (Math.Abs(det) <= double.Epsilon)
                return null;

            double inv_det = 1.0 / det;
            Vector3d T = ray.Origin - A;

            double u = Vector3d.Dot(T, P) * inv_det;

            if (u < 0.0 || u > 1.0)
                return null;

            Vector3d Q = Vector3d.Cross(T, e1);

            double v = Vector3d.Dot(ray.Direction, Q) * inv_det;
            if (v < 0.0 || u + v > 1.0)
                return null;

            double t = Vector3d.Dot(e2, Q) * inv_det;

            if (t > double.Epsilon)
                return ray.Origin + t * ray.Direction;

            return null;
        }

        public override string ToString()
        {
            return "(" + this.A + " " + this.B + " " + this.C + ")";
        }
    }
}
