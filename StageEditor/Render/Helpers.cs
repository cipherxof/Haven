using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace Haven.Render
{
    /// <summary>
    /// The sources of the input event that is raised and is generally
    /// recognized as mouse events.
    /// </summary>
    public enum MouseEventSource
    {
        /// <summary>
        /// Events raised by the mouse
        /// </summary>
        Mouse,

        /// <summary>
        /// Events raised by a stylus
        /// </summary>
        Stylus,

        /// <summary>
        /// Events raised by touching the screen
        /// </summary>
        Touch
    }

    /// <summary>
    /// Provides various helper methods.
    /// </summary>
    public class Helpers
    {
        #region Windows API declerations

        /// <summary>
        /// Gets the extra information for the mouse event.
        /// </summary>
        /// <returns>The extra information provided by Windows API</returns>
        [DllImport("user32.dll")]
        private static extern uint GetMessageExtraInfo();

        #endregion

        /// <summary>
        /// Small epsilon value
        /// </summary>
        public const double EPSILON = 1e-6;

        /// <summary>
        /// Jan, 1st, 1970 timestamp
        /// </summary>
        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Determines what input device triggered the last mouse event.
        /// (Source: https://msdn.microsoft.com/en-us/library/ms703320.aspx)
        /// </summary>
        /// <returns>
        /// A result indicating whether the last mouse event was triggered
        /// by a touch, pen or the mouse.
        /// </returns>
        public static MouseEventSource GetMouseEventSource()
        {
            uint extra = GetMessageExtraInfo();
            bool isTouchOrPen = ((extra & 0xFFFFFF00) == 0xFF515700);
            
            if (!isTouchOrPen)
                return MouseEventSource.Mouse;
            
            bool isTouch = ((extra & 0x00000080) == 0x00000080);
            return isTouch ? MouseEventSource.Touch : MouseEventSource.Stylus;
        } 

        /// <summary>
        /// Computes the timestamp based on the number of milliseconds passed
        /// since 01/01/1970
        /// </summary>
        /// <returns>The number of milliseconds passed since 01/01/1970</returns>
        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        /// <summary>
        /// Unprojects the specified point on the screen with the specified depth
        /// to the 3D space.
        /// </summary>
        /// <param name="screenLocation">The point on the screen</param>
        /// <param name="depth">The depth value in the range [0, 1] (near to far)</param>
        /// <returns>The corresponding 3D point on the screen</returns>
        public static Vector3d UnProject(Point screenLocation, double depth)
        {
            int[] viewport = GetViewportArray();
            Vector4d pos = new Vector4d();

            // Map x and y from window coordinates, map to range -1 to 1 
            pos.X = (screenLocation.X - viewport[0]) / (double)viewport[2] * 2.0f - 1.0f;
            pos.Y = 1 - (screenLocation.Y - viewport[1]) / (double)viewport[3] * 2.0f;
            pos.Z = depth * 2.0f - 1.0f;
            pos.W = 1.0f;

            Vector4d pos2 = Vector4d.Transform(pos, Matrix4d.Invert(GetModelViewMatrix() * GetProjectionMatrix()));
            Vector3d pos_out = new Vector3d(pos2.X, pos2.Y, pos2.Z);

            return pos_out / pos2.W;
        }

        /// <summary>
        /// Converts the specified screen point (in window coordinates -- origin at top left)
        /// to a world point in the OpenGL space.
        /// </summary>
        /// <param name="screenPoint">The screen point in the window coordinates</param>
        /// <param name="worldPoint">(output) The corresponding world point.</param>
        /// <returns>The depth of the screen point in the range [0, 1]</returns>
        public static double ScreenToWorldPoint(Point screenPoint, out Vector3d worldPoint)
        {
            float mouseX = screenPoint.X;
            float mouseY = GetViewportArray()[3] - screenPoint.Y;
            float depth = 0;
            GL.ReadPixels((int)mouseX, (int)mouseY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, ref depth);

            worldPoint = Helpers.UnProject(screenPoint, depth);

            return depth;
        }

        /// <summary>
        /// Converts the specified screen point (in window coordinates -- origin at top left)
        /// to a world point in the OpenGL space.
        /// </summary>
        /// <param name="screenPoint">The screen point in the window coordinates</param>
        /// <param name="worldPoint">(output) The corresponding world point.</param>
        /// <returns>The depth of the screen point in the range [0, 1]</returns>
        public static double ScreenToWorldPoint(Vector2d screenPoint, out Vector3d worldPoint)
        {
            return ScreenToWorldPoint(new Point((int)screenPoint.X, (int)screenPoint.Y), out worldPoint);
        }

        /// <summary>
        /// Constructs a Matrix4d matrix from the given array of doubles.
        /// </summary>
        /// <param name="array">The array of consecutive elements.</param>
        /// <returns>The corresponding Matrix4d instance.</returns>
        public static Matrix4d Matrix4dFromArray(double[] array)
        {
            return new Matrix4d(array[0], array[1], array[2], array[3],
                                array[4], array[5], array[6], array[7],
                                array[8], array[9], array[10], array[11],
                                array[12], array[13], array[14], array[15]);
        }

        /// <summary>
        /// Obtains the OpenGL viewport array.
        /// </summary>
        /// <returns>The OpenGL viewport array.</returns>
        public static int[] GetViewportArray()
        {
            int[] viewport = new int[4];
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, viewport);

            return viewport;
        }

        /// <summary>
        /// Obtains the OpenGL viewport rectangle.
        /// </summary>
        /// <returns>
        /// The rectangle with the same location and size as the one currently setup on OpenGL.
        /// </returns>
        public static Rectangle GetViewport()
        {
            int[] array = GetViewportArray();
            return new Rectangle(array[0], array[1], array[2], array[3]);
        }

        /// <summary>
        /// Obtains the current OpenGL projection matrix.
        /// </summary>
        /// <returns>The curren projection matrix.</returns>
        public static Matrix4d GetProjectionMatrix()
        {
            double[] projectionArray = new double[16];
            OpenTK.Graphics.OpenGL.GL.GetDouble(OpenTK.Graphics.OpenGL.GetPName.ProjectionMatrix, projectionArray);

            return Matrix4dFromArray(projectionArray);
        }

        /// <summary>
        /// Obtains the current OpenGL model view matrix.
        /// </summary>
        /// <returns>The curren model view matrix.</returns>
        public static Matrix4d GetModelViewMatrix()
        {
            double[] modelViewArray = new double[16];
            OpenTK.Graphics.OpenGL.GL.GetDouble(OpenTK.Graphics.OpenGL.GetPName.ModelviewMatrix, modelViewArray);
            return Matrix4dFromArray(modelViewArray);
        }

        /// <summary>
        /// Converts the provided screen point to ray. The screen point should be
        /// in window coordinate system (origin at top left).
        /// </summary>
        /// <param name="screenPoint">The screen point</param>
        /// <returns>The corresponding ray of the screenpoint</returns>
        public static Ray ScreenPointToRay(Point screenPoint)
        {
            Vector3d near = Helpers.UnProject(screenPoint, 0);
            Vector3d far = Helpers.UnProject(screenPoint, 1);

            return new Ray(near, far);
        }

        /// <summary>
        /// Converts the provided screen point to ray. The screen point should be
        /// in window coordinate system (origin at top left).
        /// </summary>
        /// <param name="screenPoint">The screen point</param>
        /// <returns>The corresponding ray of the screenpoint</returns>
        public static Ray ScreenPointToRay(Vector2d screenPoint)
        {
            return ScreenPointToRay(new Point((int)screenPoint.X, (int)screenPoint.Y));
        }

        /// <summary>
        /// Converts the specified mouse position from the window coordinate system to
        /// OpenGL window coordinate system (from origin at top left to origin at bottom left).
        /// </summary>
        /// <param name="mousePosition">The mouse position in window coordinate system.</param>
        /// <returns>The position in OpenGL window coordinate system.</returns>
        public static Point GetGLMouseCoordinates(Point mousePosition)
        {
            return new Point(mousePosition.X, GetViewportArray()[3] - mousePosition.Y);
        }

        /// <summary>
        /// Determines the depth of the point under the specified mouse cursor.
        /// </summary>
        /// <param name="mousePosition">The mouse position.</param>
        /// <returns>The depth value of the position.</returns>
        public static double GetDepth(Point mousePosition)
        {
            float result = 0;
            Point p = GetGLMouseCoordinates(mousePosition);
            GL.ReadPixels(p.X, p.Y, 1, 1, PixelFormat.DepthComponent, PixelType.Float, ref result);

            return result;
        }

        /// <summary>
        /// Gets the value representing the minimum depth value of the current OpenGL setup.
        /// </summary>
        /// <returns>The minimum depth value of the depth buffer.</returns>
        public static double GetMinimumDepthValue()
        {
            float[] depthRange = new float[2];
            GL.GetFloat(GetPName.DepthRange, depthRange);
            return depthRange[0];
        }

        /// <summary>
        /// Gets the value representing the maximum depth value of the current OpenGL setup.
        /// </summary>
        /// <returns>The maximum depth value of the depth buffer.</returns>
        public static double GetMaximumDepthValue()
        {
            float[] depthRange = new float[2];
            GL.GetFloat(GetPName.DepthRange, depthRange);
            return depthRange[1];
        }

        /// <summary>
        /// Selects the vector component that corresponds to the specified index.
        /// </summary>
        /// <param name="i">The index</param>
        /// <param name="vector">The vector</param>
        /// <returns>X, Y or Z component if i=0, 1, 2 respectively.</returns>
        public static double VectorComponentSelector(int i, Vector3d vector)
        {
            switch (i)
            {
                case 0:
                    return vector.X;
                case 1:
                    return vector.Y;
                case 2:
                    return vector.Z;
                default:
                    return double.NaN;
            }
        }

        /// <summary>
        /// Determines the minimum Vector3d in a collection of vertices
        /// </summary>
        /// <param name="collection">The vertex collection</param>
        /// <returns>The minimum vector in the vertices</returns>
        public static Vector3d GetMinVector3d(IEnumerable<Vector3d> collection)
        {
            double minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;

            foreach (var item in collection)
            {
                if (item.X < minX)
                    minX = item.X;
                if (item.Y < minY)
                    minY = item.Y;
                if (item.Z < minZ)
                    minZ = item.Z;
            }

            return new Vector3d(minX, minY, minZ);
        }

        /// <summary>
        /// Determines the centroid Vector3d in a collection of vertices
        /// </summary>
        /// <param name="collection">The vertex collection</param>
        /// <returns>The centroid vector in the vertices</returns>
        public static Vector3d GetCentroidVector3d(IEnumerable<Vector3d> collection)
        {
            Vector3d result = Vector3d.Zero;

            foreach (var item in collection)
                result += item;

            return result / collection.Count();
        }

        /// <summary>
        /// Determines the maximum Vector3d in a collection of vertices
        /// </summary>
        /// <param name="collection">The vertex collection</param>
        /// <returns>The maximum vector in the vertices</returns>
        public static Vector3d GetMaxVector3d(IEnumerable<Vector3d> collection)
        {
            double maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

            foreach (var item in collection)
            {
                if (item.X > maxX)
                    maxX = item.X;
                if (item.Y > maxY)
                    maxY = item.Y;
                if (item.Z > maxZ)
                    maxZ = item.Z;
            }

            return new Vector3d(maxX, maxY, maxZ);
        }

        /// <summary>
        /// Finds 3 points in the provided list that are not collinear and will
        /// return a list containing those 3 points. If no non-collinear points
        /// were found, null will be returned.
        /// </summary>
        /// <param name="list">A list of points</param>
        /// <returns>
        /// A list of 3 non-collinear points or null (of no non-collinear points were found.
        /// </returns>
        public static IList<Vector3d> FindNonCollinearPoints(IEnumerable<Vector3d> list)
        {
            int count = list.Count();

            if (count < 3)
                return null;

            Vector3d first = list.First();
            Vector3d second = list.ElementAt(1);

            // First check the last point
            Vector3d last = list.Last();

            if (!AreCollinear(first, second, last))
                return new List<Vector3d> { first, second, last };

            for (int i = 2; i < count - 1; i++)
            {
                Vector3d point = list.ElementAt(i);

                if (!AreCollinear(first, second, point))
                    return new List<Vector3d> { first, second, point };
            }

            return null;
        }

        /// <summary>
        /// Determines whether 3 points are collinear.
        /// </summary>
        /// <param name="a">The first point.</param>
        /// <param name="b">The second point.</param>
        /// <param name="c">The third point.</param>
        /// <returns>
        /// True if the points are collinear, false otherwise.
        /// </returns>
        public static bool AreCollinear(Vector3d a, Vector3d b, Vector3d c)
        {
            Vector3d vec1 = b - a;
            Vector3d vec2 = c - a;

            return vec1.Cross(vec2).LengthSquared <= EPSILON;
        }

        /// <summary>
        /// Finds the two points with the maximum distance to each other in the
        /// specified list of points. In other words, will find the diagonal of a 
        /// polygon that is defined by the specified list.
        /// </summary>
        /// <param name="list">The list of points.</param>
        /// <returns>
        /// A pair of two points which are farthest from each other.
        /// </returns>
        public static Tuple<Vector3d, Vector3d> FindFarthestPoints(IEnumerable<Vector3d> list)
        {
            int count = list.Count();

            switch (count)
            {
                case 0:
                case 1:
                    return null;
                case 2:
                    var point = list.First();
                    return new Tuple<Vector3d, Vector3d>(point, point);
            }

            double maxDistance = int.MinValue;
            Vector3d a = Vector3d.Zero;
            Vector3d b = Vector3d.Zero;

            for (int i = 0; i < count - 1; i++)
            {
                Vector3d first = list.ElementAt(i);

                for (int j = i + 1; j < count; j++)
                {
                    Vector3d second = list.ElementAt(j);
                    double distance = first.Distance(second);

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        a = first;
                        b = second;
                    }
                }
            }

            return new Tuple<Vector3d, Vector3d>(a, b);
        }

        /// <summary>
        /// Unprojects a screen point to world coordinates at the specified depth
        /// </summary>
        /// <param name="screenPoint">Screen coordinates</param>
        /// <param name="depth">Depth value (0.0 for near plane, 1.0 for far plane)</param>
        /// <param name="view">View matrix</param>
        /// <param name="projection">Projection matrix</param>
        /// <param name="viewport">Viewport [x, y, width, height]</param>
        /// <returns>World coordinates</returns>
        public static Vector3d Unproject(Point screenPoint, double depth, Matrix4d view, Matrix4d projection, int[] viewport)
        {
            double x = (2.0 * screenPoint.X) / viewport[2] - 1.0;
            double y = 1.0 - (2.0 * screenPoint.Y) / viewport[3];
            double z = 2.0 * depth - 1.0;

            Vector4d clipCoords = new Vector4d(x, y, z, 1.0);

            Matrix4d invProjection = projection.Inverted();
            Matrix4d invView = view.Inverted();

            Vector4d eyeCoords = Vector4d.Transform(clipCoords, invProjection);

            Vector4d worldCoords = Vector4d.Transform(eyeCoords, invView);

            if (Math.Abs(worldCoords.W) > 1e-6)
            {
                worldCoords /= worldCoords.W;
            }

            return new Vector3d(worldCoords.X, worldCoords.Y, worldCoords.Z);
        }

    }
}

