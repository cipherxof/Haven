using System.Drawing;

namespace Haven.Render
{
    /// <summary>
    /// A static class that is used for setting various gizmo properties (such as the color of
    /// the bounding boxes, or whether gizmos should be rendered at all).
    /// </summary>
    public static class Gizmos
    {
        /// <summary>
        /// Backing storage for the ShowAABB property.
        /// </summary>
        private static bool showAABB = false;

        /// <summary>
        /// Gets or sets the flag indicating whether the AABB should
        /// be rendered for objects or not.
        /// </summary>
        public static bool ShowAABB
        {
            get { return showAABB; }
            set { showAABB = value; }
        }

        /// <summary>
        /// The backing storage for the AABBColor property.
        /// </summary>
        private static Color aabbColor = Color.Purple;

        /// <summary>
        /// Gets or sets the default color of the AABB's.
        /// </summary>
        public static Color AABBColor
        {
            get { return aabbColor; }
            set { aabbColor = value; }
        }
    }
}
