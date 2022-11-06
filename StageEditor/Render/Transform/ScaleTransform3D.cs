using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a 3D scaling transform
    /// </summary>
    public class ScaleTransform3D : Transform3D
    {
        /// <summary>
        /// The X scale
        /// </summary>
        public double ScaleX { get; set; }

        /// <summary>
        /// The Y scale
        /// </summary>
        public double ScaleY { get; set; }

        /// <summary>
        /// The Z scale
        /// </summary>
        public double ScaleZ { get; set; }

        /// <summary>
        /// The center point of this scale transform
        /// </summary>
        public Vector3d Center { get; set; }

        /// <summary>
        /// Gets or sets the scale values of this scale transform
        /// in a vector format
        /// </summary>
        public Vector3d Scale
        {
            get
            {
                return new Vector3d(ScaleX, ScaleY, ScaleZ);
            }

            set
            {
                this.ScaleX = value.X;
                this.ScaleY = value.Y;
                this.ScaleZ = value.Z;
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
                // Apply center scaling
                transform *= Matrix4d.CreateTranslation(-Center);
                transform *= Matrix4d.Scale(ScaleX, ScaleY, ScaleZ);
                transform *= Matrix4d.CreateTranslation(Center);

                return transform;
            }
        }

        /// <summary>
        /// Initializes a new instance of ScaleTransform3D class with identity scale
        /// </summary>
        public ScaleTransform3D()
        {
            this.Scale = new Vector3d(1, 1, 1);
            this.Center = new Vector3d();
        }

        /// <summary>
        /// Initializes a new instance of ScaleTransform3D class with the provided scales
        /// and the center
        /// </summary>
        /// <param name="scaleX">The X scale</param>
        /// <param name="scaleY">The X scale</param>
        /// <param name="scaleZ">The X scale</param>
        /// <param name="center">The center of the transform</param>
        public ScaleTransform3D(double scaleX, double scaleY, double scaleZ, Vector3d center)
        {
            this.ScaleX = scaleX;
            this.ScaleY = scaleY;
            this.ScaleZ = scaleZ;
            this.Center = center;
        }

        /// <summary>
        /// Initializes a new instance of ScaleTransform3D class with the provided scales
        /// and the center
        /// </summary>
        /// <param name="scale">The scale values in vector format</param>
        /// <param name="center">The center of the transform</param>
        public ScaleTransform3D(Vector3d scale, Vector3d center) : this(scale.X, scale.Y, scale.Z, center) { }

        /// <summary>
        /// Creates a deep clone of this ScaleTransform3D instance
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            return new ScaleTransform3D(this.Scale, this.Center);
        }
    }
}
