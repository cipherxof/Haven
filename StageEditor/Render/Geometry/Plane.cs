using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a plane in 3D space.
    /// </summary>
    public class Plane
    {
        /// <summary>
        /// The point on the plane.
        /// </summary>
        public Vector3d Point { get; set; }

        /// <summary>
        /// The normal of the plane.
        /// </summary>
        public Vector3d Normal { get; set; }

        /// <summary>
        /// Initializes a new plane using the given point and normal.
        /// </summary>
        /// <param name="point">A point on the plane</param>
        /// <param name="normal">A </param>
        public Plane(Vector3d point, Vector3d normal)
        {
            normal.Normalize();

            if (normal.LengthSquared < float.Epsilon)
                throw new ArgumentException("The normal cannot be zero.");

            this.Point = point;
            this.Normal = normal.Normalized();
        }

        /// <summary>
        /// Constructs a new plane using three (non-collinear) points.
        /// If there are repeated points, or the three points are collinear
        /// ArgumentException will be thrown.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <param name="point3">The third point.</param>
        public Plane(Vector3d point1, Vector3d point2, Vector3d point3)
        {
            if (point1 == point2 || point1 == point3 || point2 == point3)
                throw new ArgumentException("The points must be unique.");

            if (Helpers.AreCollinear(point1, point2, point3))
                throw new ArgumentException("The specified points are collinear.");

            this.Point = point1;
            this.Normal = (point2 - point1).Cross(point3 - point1).Normalized();
        }

        /// <summary>
        /// Calculates the signed distance of this plane to the specified point.
        /// </summary>
        /// <param name="point">The point to calculate the distance from.</param>
        /// <returns>The signed distance between the point and this plane.</returns>
        public double SignedDistanceTo(Vector3d point)
        {
            var projected = Project(point);
            var vectorTo = point - projected;
            return vectorTo.Dot(this.Normal);
        }

        /// <summary>
        /// Calculates the unsigned (absolute) distance of this plane to the specified point.
        /// </summary>
        /// <param name="point">The point to calculate the distance from.</param>
        /// <returns>The unsigned (absolute) distance between the point and this plane.</returns>
        public double AbsoluteDistanceTo(Vector3d point)
        {
            return Math.Abs(SignedDistanceTo(point));
        }

        /// <summary>
        /// Computes the orthogonal projection of the specified point on this plane.
        /// http://mathworld.wolfram.com/Projection.html
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>The projected point on this plane.</returns>
        public Vector3d Project(Vector3d point)
        {
            double dotProduct = this.Normal.Dot(point);
            var projectionVector = (dotProduct - (Point).Dot(Normal)) * this.Normal;
            return point - projectionVector;
        }

        public override string ToString()
        {
            return "3D Plane:" + Environment.NewLine +
                "\tPoint: " + this.Point + Environment.NewLine +
                "\tNormal: " + this.Normal + Environment.NewLine;
        }

        /// <summary>
        /// Fits a 3D plane to the specified list of 3D points.
        /// </summary>
        /// <param name="collection">A collection of points to fit a 3D plane to.</param>
        /// <returns>The calculated best fit plane. Will return null if no such plane exists.</returns>
        /*public static Plane FitPlane(IEnumerable<Vector3d> collection)
        {
            int count = collection.Count();

            if (count < 3)
                throw new ArgumentException("The number of points for plane fitting cannot be less than 3.");

            // If all points are collinear, return null.
            if (Helpers.FindNonCollinearPoints(collection) == null)
                return null;

            Vector3d mean = Helpers.GetCentroidVector3d(collection);
            double[] matrixData = new double[3 * count];
            int offset = 0;

            foreach (var item in collection)
            {
                var a = item - mean;
                matrixData[offset++] = a.X;
                matrixData[offset++] = a.Y;
                matrixData[offset++] = a.Z;
            }

            var matrix = new DenseMatrix(3, count, matrixData);
            // Use SVD to compute the normal of the plane
            var svd = matrix.Svd();
            var leastSingularVec = svd.U.Column(svd.U.ColumnCount - 1);
            Vector3d normal = new Vector3d(leastSingularVec[0], leastSingularVec[1], leastSingularVec[2]);
            return new Plane(mean, normal);
        }*/

    }
}
