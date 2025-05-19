using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Render._3D
{
    public static class VectorExtensions
    {
        public static Matrix4 ToMatrix4(this Matrix4d matrix)
        {
            return new Matrix4(
                (float)matrix.M11, (float)matrix.M12, (float)matrix.M13, (float)matrix.M14,
                (float)matrix.M21, (float)matrix.M22, (float)matrix.M23, (float)matrix.M24,
                (float)matrix.M31, (float)matrix.M32, (float)matrix.M33, (float)matrix.M34,
                (float)matrix.M41, (float)matrix.M42, (float)matrix.M43, (float)matrix.M44);
        }

        public static Vector3 ToVector3(this Vector3d vector)
        {
            return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }
    }
}
