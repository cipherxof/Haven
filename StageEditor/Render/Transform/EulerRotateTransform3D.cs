using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a 3D euler angle rotation transform
    /// </summary>
    public class EulerRotateTransform3D : Transform3D
    {
        /// <summary>
        /// The X angle
        /// </summary>
        public double AngleX { get; set; }

        /// <summary>
        /// The Y angle
        /// </summary>
        public double AngleY { get; set; }

        /// <summary>
        /// The Z angle
        /// </summary>
        public double AngleZ { get; set; }

        /// <summary>
        /// The center point of this rotation transform
        /// </summary>
        public Vector3d Center { get; set; }

        /// <summary>
        /// Gets or sets the angle values of this rotation transform
        /// in a vector format
        /// </summary>
        public Vector3d Angle
        {
            get
            {
                return new Vector3d(AngleX, AngleY, AngleZ);
            }

            set
            {
                this.AngleX = value.X;
                this.AngleY = value.Y;
                this.AngleZ = value.Z;
            }
        }

        /// <summary>
        /// Gets the value of this transform in Matrix4d format
        /// </summary>
        public override Matrix4d Value
        {
            get
            {
                Matrix4d transform = Matrix4d.Identity;
                // Apply center rotation
                transform *= Matrix4d.CreateTranslation(-Center);
                transform *= Matrix4d.CreateRotationX(AngleX);
                transform *= Matrix4d.CreateRotationY(AngleY);
                transform *= Matrix4d.CreateRotationZ(AngleZ);
                transform *= Matrix4d.CreateTranslation(Center);

                return transform;
            }
        }

        /// <summary>
        /// Initializes a new instance of EulerRotateTransform3D class
        /// </summary>
        public EulerRotateTransform3D()
        {
            this.Angle = new Vector3d();
            this.Center = new Vector3d();
        }

        /// <summary>
        /// Initializes a new instance of EulerRotateTransform3D class with the specified
        /// rotation angles and the specified center
        /// </summary>
        /// <param name="angleX">The X angle</param>
        /// <param name="angleY">The Y angle</param>
        /// <param name="angleZ">The Z angle</param>
        /// <param name="center">The center of the transformation</param>
        public EulerRotateTransform3D(double angleX, double angleY, double angleZ, Vector3d center)
        {
            this.AngleX = angleX;
            this.AngleY = angleY;
            this.AngleZ = angleZ;
            this.Center = center;
        }

        /// <summary>
        /// Initializes a new instance of EulerRotateTransform3D class with the specified
        /// rotation angles and the specified center
        /// </summary>
        /// <param name="angle">The rotation angles in vector format</param>
        /// <param name="center">The center of the transformation</param>
        public EulerRotateTransform3D(Vector3d angle, Vector3d center) : this(angle.X, angle.Y, angle.Z, center) { }

        /// <summary>
        /// Creates a deep clone of this EulerRotateTransform3D instance
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            return new EulerRotateTransform3D(this.Angle, this.Center);
        }
    }
}
