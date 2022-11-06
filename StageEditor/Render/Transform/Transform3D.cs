using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Defines the parent class of all 3D transforms that can be applied to 3D objects
    /// such as translation, rotation and scaling.
    /// </summary>
    public abstract class Transform3D : ICloneable
    {
        /// <summary>
        /// Gets the identity transformation.
        /// </summary>
        public static Matrix4d Identity
        {
            get
            {
                return Matrix4d.Identity;
            }
        }

        /// <summary>
        /// Gets the value of this transform in Matrix4d format
        /// </summary>
        public abstract Matrix4d Value { get; }

        /// <summary>
        /// Transforms the given 3D vector by this Transform3D
        /// </summary>
        /// <param name="vector">The vector to be transformed</param>
        /// <returns>The transformed vector</returns>
        public Vector3d Transform(Vector3d vector)
        {
            return Vector3d.Transform(vector, this.Value);
        }

        /// <summary>
        /// Transforms the given 4D vector by this Transform3D
        /// </summary>
        /// <param name="vector">The vector to be transformed</param>
        /// <returns>The transformed vector</returns>
        public Vector4d Transform(Vector4d vector)
        {
            return Vector4d.Transform(vector, this.Value);
        }

        /// <summary>
        /// Deep clone this Transform3D instance
        /// </summary>
        /// <returns>A deep clone of this Transform3D instnace</returns>
        public abstract object Clone();
    }
}
