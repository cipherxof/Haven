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
    /// Represents a ray with origin an direction
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
                return (EndPoint - Origin).Normalized();
            }
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
        /// Computes the point on ray given the multiplier "t" such that:
        /// result = o + dt
        /// </summary>
        /// <param name="t">The placement multiplier</param>
        /// <returns>The corresponding point on the ray</returns>
        public Vector3d GetPointOnRay(double t)
        {
            return this.Origin + t * this.Direction;
        }

        /// <summary>
        /// Computes the distance between the specified point and this ray
        /// (if this ray is treated as a line).
        /// (Adapted from http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html)
        /// </summary>
        /// <param name="point">The point to calculate the distance to</param>
        /// <returns>The SQUARED distance of the point to this ray.</returns>
        public double SquaredDistance(Vector3d point)
        {
            var a = (Origin - point);
            var x = a.Dot(EndPoint - Origin);
            var dirSq = (EndPoint - Origin).LengthSquared;

            double distanceSq = (a.LengthSquared * dirSq
                 - (x * x)) / dirSq;

            return distanceSq;

        }

        /// <summary>
        /// Constructs a new ray that is expressed in terms of the object's trasnfomrs.
        /// This method is usefull for ray casting.
        /// </summary>
        /// <param name="drawable">The object to use</param>
        /// <returns>A new ray inverted by the transform of the object.</returns>
        public Ray ToObjectSpace(Drawable3D drawable)
        {
            return new Ray(Vector3d.Transform(this.Origin, drawable.Transform.Value.Inverted()), Vector3d.Transform(this.EndPoint, drawable.Transform.Value.Inverted()));
        }

        public override string ToString()
        {
            return "Origin: " + this.Origin + " Direction:" + this.Direction;
        }

        protected override void CalculateCenter()
        {
            throw new NotImplementedException();
        }

        public override void Draw()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.PushMatrix();
            Matrix4d transform = base.Transform.Value;
            GL.MultMatrix(ref transform);
            GL.Color3(Color.DarkCyan);
            GL.PointSize(5f);

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex3(this.Origin);
            GL.Vertex3(this.EndPoint);
            GL.End();
            GL.PopMatrix();
            GL.Enable(EnableCap.DepthTest);
        }

        public override HitTestResult HitTest(Ray ray)
        {
            throw new NotImplementedException();
        }
    }
}
