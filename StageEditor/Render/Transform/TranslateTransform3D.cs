using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a 3D translation transform
    /// </summary>
    public class TranslateTransform3D : Transform3D
    {
        /// <summary>
        /// The X offset
        /// </summary>
        public double OffsetX { get; set; }

        /// <summary>
        /// The Y offset
        /// </summary>
        public double OffsetY { get; set; }

        /// <summary>
        /// The Z offset
        /// </summary>
        public double OffsetZ { get; set; }

        /// <summary>
        /// Gets or sets the offset values of this translation transform
        /// in a vector format
        /// </summary>
        public Vector3d Offset
        {
            get
            {
                return new Vector3d(OffsetX, OffsetY, OffsetZ);
            }

            set
            {
                this.OffsetX = value.X;
                this.OffsetY = value.Y;
                this.OffsetZ = value.Z;
            }
        }

        /// <summary>
        /// Gets the value of this transform in Matrix4d format
        /// </summary>
        public override Matrix4d Value
        {
            get
            {
                return Matrix4d.CreateTranslation(OffsetX, OffsetY, OffsetZ);
            }
        }

        /// <summary>
        /// Initializes a new instance of TranslateTransform3D class
        /// </summary>
        public TranslateTransform3D()
        {
            this.Offset = new Vector3d();
        }

        /// <summary>
        /// Initializes a new instance of TranslateTransform3D class with the specified
        /// offsets
        /// </summary>
        /// <param name="offsetX">The X offset</param>
        /// <param name="offsetY">The Y offset</param>
        /// <param name="offsetZ">The Z offset</param>
        public TranslateTransform3D(double offsetX, double offsetY, double offsetZ)
        {
            this.OffsetX = offsetX;
            this.OffsetY = offsetY;
            this.OffsetZ = offsetZ;
        }

        /// <summary>
        /// Initializes a new instance of TranslateTransform3D class with the specified
        /// offset values in a vector format
        /// </summary>
        /// <param name="offset">The offset of the translation</param>
        public TranslateTransform3D(Vector3d offset) : this(offset.X, offset.Y, offset.Z) { }

        /// <summary>
        /// Creates a deep clone of this TranslateTransform3D instance
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            return new TranslateTransform3D(OffsetX, OffsetY, OffsetZ);
        }
    }
}
