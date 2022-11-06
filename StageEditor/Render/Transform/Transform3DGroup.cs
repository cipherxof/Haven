using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Represents a combination of multiple CodeFull.Graphics.Transform.Transform3D
    /// </summary>
    public class Transform3DGroup : Transform3D
    {
        /// <summary>
        /// Gets or sets the children of this Transform3DGroup instance
        /// </summary>
        public IList<Transform3D> Children { get; set; }

        /// <summary>
        /// Gets the result of the combination of all Transform3D's in this Transform3DGroup
        /// </summary>
        public override Matrix4d Value
        {
            get
            {
                Matrix4d transforms = Matrix4d.Identity;

                // Combine all child transforms
                foreach (var item in this.Children)
                    transforms *= item.Value;

                return transforms;
            }
        }

        /// <summary>
        /// Initializes a new instance of CodeFull.Graphics.Transform.Transform3D class.
        /// </summary>
        public Transform3DGroup()
        {
            this.Children = new List<Transform3D>();
        }

        /// <summary>
        /// Gets all the child transforms of the specified type
        /// </summary>
        /// <typeparam name="T">The type of the transform to look for (must be a subclass of Transform3D)</typeparam>
        /// <returns>A list of all the transforms of the specified type in the children of this instance</returns>
        public IEnumerable<Transform3D> GetTransforms<T>() where T : Transform3D
        {
            var temp = (from x in this.Children
                          where x.GetType() == typeof(T)
                          select x);

            return temp;
        }

        //------------------------------------------------------
        //
        //  Translation Modifiers
        //
        //------------------------------------------------------

        #region Translation Modifiers

        /// <summary>
        /// Translates this Tranform3D instance by the specified TranslateTransform3D instance.
        /// </summary>
        /// <param name="translate">The translation to be applied</param>
        public void TranslateBy(TranslateTransform3D translate)
        {
            this.Children.Add(new TranslateTransform3D(translate.OffsetX, translate.OffsetY, translate.OffsetZ));
        }

        /// <summary>
        /// Translates this Tranform3D instance by the specified offsets
        /// </summary>
        /// <param name="offsetX">The X offset</param>
        /// <param name="offsetY">The Y offset</param>
        /// <param name="offsetZ">The Z offset</param>
        public void TranslateBy(double offsetX, double offsetY, double offsetZ)
        {
            this.TranslateBy(new Vector3d(offsetX, offsetY, offsetZ));
        }

        /// <summary>
        /// Translates this Tranform3D instance by the offsets specified by the Vector3d instance
        /// </summary>
        /// <param name="offset">The offsets of each axis in a vector format</param>
        public void TranslateBy(Vector3d offset)
        {
            this.TranslateBy(new TranslateTransform3D(offset));
        }

        /// <summary>
        /// Sets the traslation of this Tranform3D instance to the specified TranslateTransform3D instance.
        /// </summary>
        /// <param name="translate">The translation to be applied</param>
        public void SetTranslation(TranslateTransform3D translate)
        {
            // Find and remove all translation transforms
            var translations = GetTransforms<TranslateTransform3D>();

            this.Children.Clear();
            this.Children.Add(new TranslateTransform3D(translate.OffsetX, translate.OffsetY, translate.OffsetZ));
        }

        /// <summary>
        /// Sets the traslation of this Tranform3D instance to the specified offsets
        /// </summary>
        /// <param name="offsetX">The X offset</param>
        /// <param name="offsetY">The Y offset</param>
        /// <param name="offsetZ">The Z offset</param>
        public void SetTranslation(double offsetX, double offsetY, double offsetZ)
        {
            this.SetTranslation(new Vector3d(offsetX, offsetY, offsetZ));
        }

        /// <summary>
        /// Sets the traslation of this Tranform3D instance to the specified Vector3d instance
        /// </summary>
        /// <param name="offset">The offsets of each axis in a vector format</param>
        public void SetTranslation(Vector3d offset)
        {
            this.SetTranslation(new TranslateTransform3D(offset));
        }

        #endregion

        //------------------------------------------------------
        //
        //  Rotation Modifiers
        //
        //------------------------------------------------------

        #region Rotation Modifiers

        /// <summary>
        /// Rotates this Tranform3D instance by the specified RotateTransform3D instance.
        /// </summary>
        /// <param name="rotation">The rotation to be applied</param>
        public void RotateBy(EulerRotateTransform3D rotation)
        {
            this.Children.Add(new EulerRotateTransform3D(rotation.AngleX, rotation.AngleY, rotation.AngleZ, rotation.Center));
        }

        /// <summary>
        /// Rotates this Tranform3D instance by the angles specified by the Vector3d instance and around
        /// the specified center
        /// </summary>
        /// <param name="angle">The angles of each axis in a vector format</param>
        /// <param name="center">The center of the rotation transform</param>
        public void RotateBy(Vector3d angle, Vector3d center)
        {
            this.RotateBy(new EulerRotateTransform3D(angle, center));
        }

        /// <summary>
        /// Rotates this Tranform3D instance by the specified angles
        /// </summary>
        /// <param name="angleX">The X angle</param>
        /// <param name="angleY">The Y angle</param>
        /// <param name="angleZ">The Z angle</param>
        /// <param name="center">The center of the rotation</param>
        public void RotateBy(double angleX, double angleY, double angleZ, Vector3d center)
        {
            this.RotateBy(new Vector3d(angleX, angleY, angleZ), center);
        }

        /// <summary>
        /// Sets the rotation of this Tranform3D instance to the specified RotateTransform3D instance.
        /// </summary>
        /// <param name="rotation">The rotation to be applied</param>
        public void SetRotation(EulerRotateTransform3D rotation)
        {
            // Find and remove all rotation transforms
            var rotations = GetTransforms<EulerRotateTransform3D>();

            foreach (var item in rotations)
                this.Children.Remove(item);

            this.Children.Add(new EulerRotateTransform3D(rotation.AngleX, rotation.AngleY, rotation.AngleZ, rotation.Center));
        }

        /// <summary>
        /// Sets the rotation of this Tranform3D instance to the specified angles
        /// </summary>
        /// <param name="angleX">The X angle</param>
        /// <param name="angleY">The Y angle</param>
        /// <param name="angleZ">The Z angle</param>
        /// <param name="center">The center of the rotation</param>
        public void SetRotation(double angleX, double angleY, double angleZ, Vector3d center)
        {
            this.SetRotation(new Vector3d(angleX, angleY, angleZ), center);
        }

        /// <summary>
        /// Sets the rotation of this Tranform3D instance to the specified Vector3d instance
        /// </summary>
        /// <param name="offset">The angle of each axis in a vector format</param>
        /// <param name="center">The center of the rotation</param>
        public void SetRotation(Vector3d offset, Vector3d center)
        {
            this.SetRotation(new EulerRotateTransform3D(offset, center));
        }

        #endregion

        //------------------------------------------------------
        //
        //  Scaling Modifiers
        //
        //------------------------------------------------------

        #region Scaling Modifiers

        /// <summary>
        /// Scales this Tranform3D instance by the specified ScaleTransform3D instance.
        /// </summary>
        /// <param name="scale">The scale to be applied</param>
        public void ScaleBy(ScaleTransform3D scale)
        {
            // Have to add 1 to make the transformation additive!
            this.Children.Add(new ScaleTransform3D(scale.ScaleX + 1, scale.ScaleY + 1, scale.ScaleZ + 1, scale.Center));
        }

        /// <summary>
        /// Scales this Tranform3D instance by the amounts specified by the Vector3d instance and around
        /// the specified center
        /// </summary>
        /// <param name="scale">The scales of each axis in a vector format</param>
        /// <param name="center">The center of the scale transform</param>
        public void ScaleBy(Vector3d scale, Vector3d center)
        {
            this.ScaleBy(new ScaleTransform3D(scale, center));
        }

        /// <summary>
        /// Scale this Tranform3D instance by the specified amounts
        /// </summary>
        /// <param name="scaleX">The X scale</param>
        /// <param name="scaleY">The Y scale</param>
        /// <param name="scaleZ">The Z scale</param>
        /// <param name="center">The center of the scaling</param>
        public void ScaleBy(double scaleX, double scaleY, double scaleZ, Vector3d center)
        {
            this.ScaleBy(new Vector3d(scaleX, scaleY, scaleZ), center);
        }

        /// <summary>
        /// Sets the scale of this Tranform3D instance to the specified ScaleTransform3D instance.
        /// </summary>
        /// <param name="scale">The scale to be applied</param>
        public void SetScale(ScaleTransform3D scale)
        {
            // Find and remove all scale transforms
            var scales = GetTransforms<ScaleTransform3D>();

            foreach (var item in scales)
                this.Children.Remove(item);

            this.Children.Add(new ScaleTransform3D(scale.ScaleX, scale.ScaleY, scale.ScaleZ, scale.Center));
        }

        /// <summary>
        /// Sets the scale of this Tranform3D instance to the specified offsets
        /// </summary>
        /// <param name="scaleX">The X scale</param>
        /// <param name="scaleY">The Y scale</param>
        /// <param name="scaleZ">The Z scale</param>
        /// <param name="center">The center of the scale</param>
        public void SetScale(double scaleX, double scaleY, double scaleZ, Vector3d center)
        {
            this.SetScale(new Vector3d(scaleX, scaleY, scaleZ), center);
        }

        /// <summary>
        /// Sets the scale of this Tranform3D instance to the specified Vector3d instance
        /// </summary>
        /// <param name="offset">The scale of each axis in a vector format</param>
        /// <param name="center">The center of the scale</param>
        public void SetScale(Vector3d offset, Vector3d center)
        {
            this.SetScale(new ScaleTransform3D(offset, center));
        }

        #endregion

        /// <summary>
        /// Creates a deep clone of this Transform3DGroup instance with all of its children
        /// </summary>
        /// <returns>A deep clone of this transform</returns>
        public override object Clone()
        {
            Transform3DGroup result = new Transform3DGroup();

            foreach (var item in this.Children)
                result.Children.Add(item.Clone() as Transform3D);

            return result;
        }
    }
}
