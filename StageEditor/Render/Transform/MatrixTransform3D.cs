using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a 3D matrix transform 
    /// </summary>
    public class MatrixTransform3D : Transform3D
    {
        /// <summary>
        /// Gets or sets the transformation matrix of this MatrixTransform3D instance
        /// </summary>
        public Matrix4d Matrix { get; set; }

        /// <summary>
        /// Gets the value of this transform in Matrix4d format
        /// </summary>
        public override Matrix4d Value
        {
            get { return this.Matrix; }
        }

        /// <summary>
        /// Initializes a new instance of MatrixTransform3D class with identity transformation
        /// </summary>
        public MatrixTransform3D()
        {
            this.Matrix = Identity;
        }

        /// <summary>
        /// Initializes a new instance of MatrixTransform3D class with the specified matrix
        /// </summary>
        /// <param name="transform"></param>
        public MatrixTransform3D(Matrix4d transform) : this()
        {
            this.Matrix = transform;
        }

        /// <summary>
        /// Creates a deep clone of this MatrixTransform3D instance
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            return new MatrixTransform3D(this.Matrix);
        }
    }
}
