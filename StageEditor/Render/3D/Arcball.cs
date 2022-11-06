using OpenTK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Haven.Render
{
    /// <summary>
    /// Arcball is used to implement intuitive rotations using the mouse.
    /// This implementation is based on NeHe's implementation for C++
    /// </summary>
    public class Arcball
    {
        /// <summary>
        /// The last set mouse cursor position
        /// </summary>
        private Point mousePosition;

        /// <summary>
        /// Start of the click vector (mapped to the sphere)
        /// </summary>
        private Vector3d clickStartVector;

        /// <summary>
        /// End of the click vector (mapped to the sphere)
        /// </summary>
        private Vector3d clickEndVector;

        /// <summary>
        /// Adjusted mouse bounds width
        /// </summary>
        private double adjustedWidth;

        /// <summary>
        /// Adjusted mouse bounds height
        /// </summary>
        private double adjustedHeight;

        /// <summary>
        /// The height of the OpenGL canvas
        /// </summary>
        private int height;

        /// <summary>
        /// A mapping of the mouse button to their pressed status
        /// </summary>
        private IDictionary<MouseButtons, bool> buttonMapping = new Dictionary<MouseButtons, bool>();

        /// <summary>
        /// The sensitivity of this arcball (default is 0.01)
        /// </summary>
        public double Sensitivity { get; set; }

        /// <summary>
        /// The drawable that this arcball instance performs on
        /// </summary>
        public Drawable3D Drawable { get; set; }

        /// <summary>
        /// Instantiates a new Arcball with the specified boundaries
        /// for the width and height
        /// </summary>
        /// <param name="width">The width</param>
        /// <param name="height">The height</param>
        /// <param name="sensitivity">The sensitivity of the trackball</param>
        public Arcball(int width, int height, double sensitivity = 0.01)
        {
            this.Sensitivity = sensitivity;
            this.Drawable = null;
            clickStartVector = new Vector3d();
            clickEndVector = new Vector3d();
            SetBounds(width, height);

            buttonMapping[MouseButtons.Left] = false;
            buttonMapping[MouseButtons.Middle] = false;
            buttonMapping[MouseButtons.Right] = false;
        }

        /// <summary>
        /// Handles the operations that need to be performed when the mouse button
        /// is pressed.
        /// </summary>
        /// <param name="e">The event arguments</param>
        public void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                this.SetMouseButtonStatus(MouseButtons.Left, true);
                this.SetMousePosition(e.Location);
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Middle)
            {
                this.SetMousePosition(e.Location);
                this.SetMouseButtonStatus(MouseButtons.Middle, true);
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                this.SetMousePosition(e.Location);
                this.SetMouseButtonStatus(MouseButtons.Right, true);
            }
        }

        /// <summary>
        /// Handles the operations that need to be performed when the mouse button
        /// is released.
        /// </summary>
        /// <param name="e">The event arguments</param>
        public void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                this.SetMouseButtonStatus(MouseButtons.Left, false);
            if (e.Button == System.Windows.Forms.MouseButtons.Middle)
                this.SetMouseButtonStatus(MouseButtons.Middle, false);
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
                this.SetMouseButtonStatus(MouseButtons.Right, false);
        }

        /// <summary>
        /// Maps the given point to the sphere and returns the resulting vector
        /// </summary>
        /// <param name="point">The point to map to sphere</param>
        /// <returns>The vector of the mapped point</returns>
        private Vector3d MapToSphere(Point point)
        {
            Vector3d result = new Vector3d();

            PointF tempPoint = new PointF(point.X, point.Y);

            //Adjust point coords and scale down to range of [-1 ... 1]
            tempPoint.X = (float)(tempPoint.X * this.adjustedWidth) - 1.0f;
            tempPoint.Y = (float)(1.0f - (tempPoint.Y * this.adjustedHeight));

            //Compute square of the length of the vector from this point to the center
            float length = (tempPoint.X * tempPoint.X) + (tempPoint.Y * tempPoint.Y);

            //If the point is mapped outside the sphere... (length > radius squared)
            if (length > 1.0f)
            {
                //Compute a normalizing factor (radius / sqrt(length))
                float norm = (float)(1.0 / Math.Sqrt(length));

                //Return the "normalized" vector, a point on the sphere
                result.X = tempPoint.X * norm;
                result.Y = tempPoint.Y * norm;
                result.Z = 0.0f;
            }
            //Else it's inside
            else
            {
                //Return a vector to a point mapped inside the sphere sqrt(radius squared - length)
                result.X = tempPoint.X;
                result.Y = tempPoint.Y;
                result.Z = (float)System.Math.Sqrt(1.0f - length);
            }

            return result;
        }

        /// <summary>
        /// Set the boundaries of the mouse click
        /// </summary>
        /// <param name="width">The width boundary</param>
        /// <param name="height">The height boundary</param>
        public void SetBounds(int width, int height)
        {
            //Set adjustment factor for width/height
            this.adjustedWidth = 1.0 / ((width - 1.0) * 0.5);
            this.adjustedHeight = 1.0 / ((height - 1.0) * 0.5);
            this.height = height;
        }

        /// <summary>
        /// Sets the pressed status of the specified mouse button
        /// </summary>
        /// <param name="button">The mouse button to set</param>
        /// <param name="isPressed">The pressed status of that button</param>
        public void SetMouseButtonStatus(MouseButtons button, bool isPressed)
        {
            this.buttonMapping[button] = isPressed;
        }

        /// <summary>
        /// Sets the start position of the mouse
        /// </summary>
        /// <param name="position"></param>
        public void SetMousePosition(Point position)
        {
            this.mousePosition = position;
            this.clickStartVector = MapToSphere(position);
        }

        /// <summary>
        /// Calculate the rotation for the current point
        /// </summary>
        /// <param name="currentPoint"></param>
        /// <returns></returns>
        protected Quaterniond GetRotation(Point currentPoint)
        {
            Quaterniond result = Quaterniond.Identity; // Must be identity! Not zero!!

            //Map the point to the sphere
            this.clickEndVector = this.MapToSphere(currentPoint);

            //Return the quaternion equivalent to the rotation
            //Compute the vector perpendicular to the begin and end vectors
            Vector3d Perp = Vector3d.Cross(clickStartVector, clickEndVector);

            //Compute the length of the perpendicular vector
            if (Perp.Length > double.Epsilon)
            //if its non-zero
            {
                //We're ok, so return the perpendicular vector as the transform after all
                result.X = Perp.X;
                result.Y = Perp.Y;
                result.Z = Perp.Z;
                //In the quaternion values, w is cosine (theta / 2), where theta is the rotation angle
                result.W = Vector3d.Dot(clickStartVector, clickEndVector);
            }

            return result;
        }

        /// <summary>
        /// Applies all the transformations possible based on the current status of mouse buttons
        /// </summary>
        /// <param name="currentCursorPosition">The current position of the mouse cursor</param>
        public virtual void ApplyTransforms(Point currentCursorPosition)
        {
            if (this.Drawable == null)
                return;

            if (this.buttonMapping[MouseButtons.Left])
            {
                // Convert current and previous mouse positions to OpenGL window coordinates
                Point prevPosition = new Point(this.mousePosition.X, this.height - this.mousePosition.Y);
                Point currentPosition = new Point(currentCursorPosition.X, this.height - currentCursorPosition.Y);

                int deltaX = currentPosition.X - prevPosition.X;
                int deltaY = currentPosition.Y - prevPosition.Y;

                var keyboard = OpenTK.Input.Keyboard.GetState();

                if (keyboard.IsKeyDown(OpenTK.Input.Key.ControlLeft) || keyboard.IsKeyDown(OpenTK.Input.Key.ControlRight))
                {
                    this.Drawable.Transform.TranslateBy(0, 0, -deltaY * Sensitivity * 3);
                }
                else
                    this.Drawable.Transform.TranslateBy(deltaX * Sensitivity, deltaY * Sensitivity, 0);
            }

            if (this.buttonMapping[MouseButtons.Middle])
            {
                // Convert current and previous mouse positions to OpenGL window coordinates
                Point prevPosition = new Point(this.mousePosition.X, this.height - this.mousePosition.Y);
                Point currentPosition = new Point(currentCursorPosition.X, this.height - currentCursorPosition.Y);

                double scale = (currentPosition.X - prevPosition.X) * Sensitivity;
                this.Drawable.Transform.ScaleBy(scale, scale, scale, Drawable.TransformedCenter);
            }

            if (this.buttonMapping[MouseButtons.Right])
            {
                Quaterniond newRot = GetRotation(currentCursorPosition);
                Drawable.Transform.Children.Add(new QuaternionRotateTransform3D(newRot, Drawable.TransformedCenter));
            }

            // Update the cursor position
            SetMousePosition(currentCursorPosition);
        }
    }
}
