using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a ray with origin and direction
    /// </summary>
    public class Ray : Drawable3D
    {
        /// <summary>
        /// Gets or sets the origin of this ray
        /// </summary>
        public Vector3d Origin { get; set; }

        /// <summary>
        /// Gets or sets the end point of this ray
        /// </summary>
        public Vector3d EndPoint { get; set; }

        /// <summary>
        /// Gets the normalized direction of this ray
        /// </summary>
        public Vector3d Direction
        {
            get
            {
                var dir = EndPoint - Origin;
                return dir.Length > 1e-6 ? dir.Normalized() : Vector3d.UnitZ;
            }
        }

        /// <summary>
        /// Gets the length of this ray
        /// </summary>
        public double Length
        {
            get { return (EndPoint - Origin).Length; }
        }

        /// <summary>
        /// Creates a new ray using the specified origin and end point.
        /// </summary>
        /// <param name="origin">The origin of the ray.</param>
        /// <param name="endPoint">The end point of the ray.</param>
        public Ray(Vector3d origin, Vector3d endPoint)
        {
            this.Origin = origin;
            this.EndPoint = endPoint;
        }

        /// <summary>
        /// Creates a new ray using origin and direction
        /// </summary>
        /// <param name="origin">The origin of the ray</param>
        /// <param name="direction">The direction of the ray</param>
        /// <param name="length">The length of the ray</param>
        public Ray(Vector3d origin, Vector3d direction, double length)
        {
            this.Origin = origin;
            this.EndPoint = origin + direction.Normalized() * length;
        }

        /// <summary>
        /// Transforms this ray instance using the specified Transform3D.
        /// </summary>
        /// <param name="transform">The transform to apply to this ray.</param>
        public void DoTransform(Transform3D transform)
        {
            DoTransform(transform.Value);
        }

        /// <summary>
        /// Transforms this ray instance using the specified transformation matrix.
        /// </summary>
        /// <param name="matrix">The transformation matrix to apply to this ray.</param>
        public void DoTransform(Matrix4d matrix)
        {
            this.Origin = Vector3d.Transform(this.Origin, matrix);
            this.EndPoint = Vector3d.Transform(this.EndPoint, matrix);
        }

        /// <summary>
        /// Computes the point on ray given the parameter "t" such that:
        /// result = origin + t * direction
        /// </summary>
        /// <param name="t">The parameter along the ray</param>
        /// <returns>The corresponding point on the ray</returns>
        public Vector3d GetPointOnRay(double t)
        {
            return this.Origin + t * this.Direction;
        }

        /// <summary>
        /// Computes the squared distance between the specified point and this ray
        /// </summary>
        /// <param name="point">The point to calculate the distance to</param>
        /// <returns>The squared distance of the point to this ray.</returns>
        public double SquaredDistance(Vector3d point)
        {
            var toPoint = point - Origin;
            var direction = Direction;

            // Project toPoint onto the ray direction
            double proj = toPoint.Dot(direction);

            // Clamp to ray segment if needed
            proj = Math.Max(0, Math.Min(proj, Length));

            // Find closest point on ray
            var closestPoint = Origin + proj * direction;

            // Return squared distance
            return (point - closestPoint).LengthSquared;
        }

        /// <summary>
        /// Constructs a new ray that is expressed in the object's local space.
        /// This method is useful for ray casting against transformed objects.
        /// </summary>
        /// <param name="drawable">The object to transform into</param>
        /// <returns>A new ray transformed into the object's local space.</returns>
        public Ray ToObjectSpace(Drawable3D drawable)
        {
            var invTransform = drawable.Transform.Value.Inverted();
            return new Ray(
                Vector3d.Transform(this.Origin, invTransform),
                Vector3d.Transform(this.EndPoint, invTransform)
            );
        }

        public override string ToString()
        {
            return $"Ray: Origin={Origin}, Direction={Direction}, Length={Length:F3}";
        }

        protected override void CalculateCenter()
        {
            Center = (Origin + EndPoint) * 0.5;
        }

        public override void Draw()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.PushMatrix();
            Matrix4d transform = base.Transform.Value;
            GL.MultMatrix(ref transform);
            GL.Color3(Color.DarkCyan);
            GL.LineWidth(2.0f);

            GL.Begin(PrimitiveType.Lines);
            GL.Vertex3(this.Origin);
            GL.Vertex3(this.EndPoint);
            GL.End();

            // Draw origin point
            GL.PointSize(5f);
            GL.Begin(PrimitiveType.Points);
            GL.Vertex3(this.Origin);
            GL.End();

            GL.PopMatrix();
            GL.Enable(EnableCap.DepthTest);
        }

        public override HitTestResult HitTest(Ray ray)
        {
            return null;
        }
    }
}
