using OpenTK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// Defines the interface for objects that can be drawn using OpenGL
    /// </summary>
    public abstract class Drawable3D
    {
        /// <summary>
        /// Gets or sets the list of drawables attached to this CodeFull.Graphics.Drawable instance
        /// </summary>
        public Drawable3DCollection Attachments { get; set; }

        /// <summary>
        /// Internally stores the centroid of this drawable
        /// </summary>
        protected Vector3d center;

        public bool Visible = true;

        /// <summary>
        /// Gets the center point of this drawable
        /// </summary>
        public Vector3d Center
        {
            get { return this.center; }
        }

        /// <summary>
        /// Gets the center point of this Drawable after performing all the transformations on.
        /// </summary>
        public Vector3d TransformedCenter
        {
            get
            {
                return this.Transform.Transform(this.Center);
            }
        }

        /// <summary>
        /// The collection of all transforms applied to this Drawable.
        /// </summary>
        protected Transform3DGroup transform = new Transform3DGroup();

        /// <summary>
        /// Gets or sets the collection of the transforms applied on this
        /// Drawable.
        /// </summary>
        public Transform3DGroup Transform
        {
            get { return this.transform; }
            set { this.transform = value; }
        }

        /// <summary>
        /// The parent of this Drawable
        /// </summary>
        public Drawable3D Parent { get; set; }

        /// <summary>
        /// Gets the axis-aligned bounding box of this Drawable.
        /// </summary>
        public AABB AABB {get; protected set;}

        /// <summary>
        /// Gets or sets the value indicating whether the AABB is rendered.
        /// </summary>
        public bool ShowAABB { get; set; }

        /// <summary>
        /// A method to calculate the centroid of this Drawable
        /// </summary>
        protected abstract void CalculateCenter();

        /// <summary>
        /// Draws the contents of this Drawable using OpenGL
        /// </summary>
        public abstract void Draw();

        /// <summary>
        /// Performs a ray casting hit test using the specified ray. 
        /// </summary>
        /// <param name="ray">The ray to perform hit test for</param>
        /// <returns>The result of the hit test (if any hit occurred), null otherwise.</returns>
        public abstract HitTestResult HitTest(Ray ray);

        /// <summary>
        /// Performs a ray casting hit test using the specified point on the screen.
        /// By default, the screen point will be converted to a ray and the raycasting will be performed.
        /// Note that the ray will be transformed to the object's space.
        /// </summary>
        /// <param name="screenPoint">The screen point to perform hit test for</param>
        /// <returns>The result of the hit test (if any hit occurred), null otherwise.</returns>
        public virtual HitTestResult HitTest(Point screenPoint)
        {
            return HitTest(Helpers.ScreenPointToRay(screenPoint).ToObjectSpace(this));
        }
    }
}
