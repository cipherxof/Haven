using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a 3D rotation transform by a quaternion
    /// </summary>
    public class QuaternionRotateTransform3D : Transform3D
    {
        /// <summary>
        /// Gets or sets the rotation of this QuaternionRotateTransform3D
        /// </summary>
        public Quaterniond Rotation { get; set; }

        /// <summary>
        /// The center point of this rotation transform
        /// </summary>
        public Vector3d Center { get; set; }

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
                transform *= Matrix4d.Rotate(this.Rotation);
                transform *= Matrix4d.CreateTranslation(Center);

                return transform;
            }
        }

        /// <summary>
        /// Initializes a new instance of QuaternionRotateTransform3D class with identity rotation
        /// </summary>
        public QuaternionRotateTransform3D()
        {
            this.Rotation = Quaterniond.Identity;
        }

        /// <summary>
        /// Initializes a new instance of QuaternionRotateTransform3D class with the specified
        /// rotation and center
        /// </summary>
        /// <param name="rotation"></param>
        /// <param name="center"></param>
        public QuaternionRotateTransform3D(Quaterniond rotation, Vector3d center)
        {
            this.Rotation = rotation;
            this.Center = center;
        }

        /// <summary>
        /// Creates a deep clone of this QuaternionRotateTransform3D instance
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            return new QuaternionRotateTransform3D(this.Rotation, this.Center);
        }
    }
}
