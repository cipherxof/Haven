using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    public class Line
    {
        /// <summary>
        /// Tolerance for orthogonality check. (This tolerance results in a very loose check)
        /// </summary>
        protected const double ORTHOGONALITY_TOLERANCE = 0.3;

        /// <summary>
        /// Tolerance for parallelism check.
        /// </summary>
        protected const double PARALLELISM_TOLERANCE = 0.9;

        /// <summary>
        /// Gets or sets the point of this line.
        /// </summary>
        public Vector3d Point { get; set; }

        /// <summary>
        /// Gets or sets the normalized direction of this line.
        /// </summary>
        public Vector3d Direction { get; set; }

        /// <summary>
        /// Constructs a new line using either a point and a direction vector or
        /// using two points.
        /// </summary>
        /// <param name="point">The point on the line.</param>
        /// <param name="vectorOrPoint">Either the vector of the line or another point on the line</param>
        /// <param name="isDirection">Determines whether the second argument was the direction vector or not.</param>
        public Line(Vector3d point, Vector3d vectorOrPoint, bool isDirection = true)
        {
            this.Point = point;
            if (isDirection)
                this.Direction = vectorOrPoint.Normalized();
            else
                this.Direction = (vectorOrPoint - point).Normalized();
        }

        /// <summary>
        /// Computes the squared distance between the specified point and this line
        /// (Adapted from http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html)
        /// </summary>
        /// <param name="point">The point to calculate the distance to</param>
        /// <returns>The squared distance of the point to this line.</returns>
        public double SquaredDistanceTo(Vector3d point)
        {
            return Direction.Cross(Point - point).LengthSquared / Direction.LengthSquared;
        }

        /// <summary>
        /// Computes the distance between the specified point and this line
        /// (Adapted from http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html)
        /// </summary>
        /// <param name="point">The point to calculate the distance to</param>
        /// <returns>The distance of the point to this line.</returns>
        public double DistanceTo(Vector3d point)
        {
            return Math.Sqrt(SquaredDistanceTo(point));
        }

        public override string ToString()
        {
            return "3D Line:" + Environment.NewLine +
                "\tPoint: " + this.Point + Environment.NewLine +
                "\tDirection: " + this.Direction + Environment.NewLine;
        }

        /// <summary>
        /// Computes the 3D Orthogonal Distance Regression (ODR) line for the
        /// 3D points in the provided collection.
        /// </summary>
        /// <param name="collection">The collection to process</param>
        /// <returns>The 3D ODR line.</returns>
        /*public static Line LeastSquaresFit(IEnumerable<Vector3d> collection)
        {
            Vector3d mean = Helpers.GetCentroidVector3d(collection);
            int count = collection.Count();
            double[] matrixData = new double[3 * count];
            int offset = 0;

            foreach (var item in collection)
            {
                var a = item - mean;
                matrixData[offset++] = a.X;
                matrixData[offset++] = a.Y;
                matrixData[offset++] = a.Z;
            }

            var matrix = new DenseMatrix(3, count, matrixData).Transpose();
            // Obtain the direction vector
            var topSvd = matrix.Svd().VT.Row(0);
            Vector3d direction = new Vector3d(topSvd[0], topSvd[1], topSvd[2]).Normalized();
            return new Line(mean, direction);
        }*/

        /// <summary>
        /// Determines whether this line is orthogonal to the specified line.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <returns>True if the lines are orthogonal, false otherwise.</returns>
        public bool IsOrthogonalTo(Line other)
        {
            return Math.Abs(this.Direction.Dot(other.Direction)) < ORTHOGONALITY_TOLERANCE;
        }

        /// <summary>
        /// Determines whether this line is parallel to the specified line.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <returns>True if the lines are parallel, false otherwise.</returns>
        public bool IsParallelTo(Line other)
        {
            double dot = Direction.Dot(other.Direction);
            return (Math.Abs(dot) >= PARALLELISM_TOLERANCE);
        }

        /// <summary>
        /// Computes the angle between this line and another line in radians.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <returns>The angle in the range 0 ≤ θ ≤ π</returns>
        public double AngleBetween(Line other)
        {
            return Math.Acos(Direction.Dot(other.Direction));
        }

        /// <summary>
        /// Computes the angle between this line and a ray.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <returns>The angle in the range 0 ≤ θ ≤ π</returns>
        public double AngleBetween(Ray ray)
        {
            return Math.Acos(Direction.Dot(ray.Direction));
        }
    }
}
