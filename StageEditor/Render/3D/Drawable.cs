using OpenTK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Haven.Render
{

    public abstract class Drawable3D
    {

        public Drawable3DCollection Attachments { get; set; }

        protected Vector3d center;

        public bool Visible = true;

        public Vector3d Center
        {
            get { return this.center; }
            set { this.center = value; }
        }

        public Vector3d TransformedCenter
        {
            get
            {
                return this.Transform.Transform(this.Center);
            }
        }

        protected Transform3DGroup transform = new Transform3DGroup();

        public Transform3DGroup Transform
        {
            get { return this.transform; }
            set { this.transform = value; }
        }

        public Drawable3D Parent { get; set; }

        public AABB AABB { get; protected set; }

        public bool ShowAABB { get; set; }

        protected abstract void CalculateCenter();

        public abstract void Draw();

        public abstract HitTestResult HitTest(Ray ray);

        public virtual HitTestResult HitTest(Point screenPoint)
        {
            Ray worldRay = Helpers.ScreenPointToRay(screenPoint);
            Ray objectRay = worldRay.ToObjectSpace(this);
            return HitTest(objectRay);
        }
    }
}