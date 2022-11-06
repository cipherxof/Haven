using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haven.Render
{
    /// <summary>
    /// A collection of Drawable objects
    /// </summary>
    public class Drawable3DCollection : ICollection<Drawable3D>, IList<Drawable3D>, IEnumerable<Drawable3D>
    {
        /// <summary>
        /// The underlying collection of the drawables
        /// </summary>
        protected IList<Drawable3D> collection = new List<Drawable3D>();

        /// <summary>
        /// The owner of this collection
        /// </summary>
        protected Drawable3D owner;

        protected void ApplyTransform(Drawable3D item)
        {
            // TODO keep the objects transforms and append the parent's
            item.Transform = this.owner.Transform;
        }

        /// <summary>
        /// Creates a new DrawableCollection instance using the specified drawable as the owner.
        /// </summary>
        /// <param name="owner">The ownder of this drawable.</param>
        public Drawable3DCollection(Drawable3D owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Add a new Drawable to this collection.
        /// </summary>
        /// <param name="item">The Drawable instance to add.</param>
        public void Add(Drawable3D item)
        {
            this.collection.Add(item);
            item.Parent = owner;
            ApplyTransform(item);
        }

        /// <summary>
        /// Clear this collection.
        /// </summary>
        public void Clear()
        {
            foreach (var item in collection)
                item.Parent = null;

            this.collection.Clear();
        }

        /// <summary>
        /// Determines whether the specified Drawable instance exists in this
        /// DrawableCollection.
        /// </summary>
        /// <param name="item">The Drawable instance to look for.</param>
        /// <returns>true if the collection contains the specified Drawable. false otherwise.</returns>
        public bool Contains(Drawable3D item)
        {
            return this.collection.Contains(item);
        }
        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional Array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(Drawable3D[] array, int arrayIndex)
        {
            this.collection.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of Drawables in this collection.
        /// </summary>
        public int Count
        {
            get { return this.collection.Count; }
        }

        /// <summary>
        /// Gets the value determining whether this collection is read only or not.
        /// </summary>
        public bool IsReadOnly
        {
            get { return this.collection.IsReadOnly; }
        }

        /// <summary>
        /// Removes the specified item from this collection (if it exists).
        /// </summary>
        /// <param name="item">The Drawable to remove.</param>
        /// <returns>true if the specified Drawable existed and was removed. false otherwise.</returns>
        public bool Remove(Drawable3D item)
        {
            item.Parent = null;
            return this.collection.Remove(item);
        }

        /// <summary>
        /// Gets the enumerator of this collection.
        /// </summary>
        /// <returns>This collections enumerator.</returns>
        public IEnumerator<Drawable3D> GetEnumerator()
        {
            return this.collection.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.collection.GetEnumerator();
        }

        /// <summary>
        /// Determines the index of the specified Drawable in this collection.
        /// </summary>
        /// <param name="item">The Drawable.</param>
        /// <returns>The index of the Drawable if it exists in this collection. -1 otherwise.</returns>
        public int IndexOf(Drawable3D item)
        {
            return this.collection.IndexOf(item);
        }

        /// <summary>
        /// Inserts the speicfied Drawable at the specified index in this collection.
        /// </summary>
        /// <param name="index">The place to insert the Drawable.</param>
        /// <param name="item">The Drawable to insert.</param>
        public void Insert(int index, Drawable3D item)
        {
            this.collection.Insert(index, item);
            item.Parent = this.owner;
            ApplyTransform(item);
        }

        /// <summary>
        /// Removes the Drawable instance at the specified index.
        /// </summary>
        /// <param name="index">The index of the Drawable to remove.</param>
        public void RemoveAt(int index)
        {
            try
            {
                collection[index].Parent = null;
            }
            catch (Exception) { }

            this.collection.RemoveAt(index);
        }

        /// <summary>
        /// Gets or sets the Drawbale at the specified index in this collection.
        /// </summary>
        /// <param name="index">The access index.</param>
        /// <returns>The Drawable at the specified index.</returns>
        public Drawable3D this[int index]
        {
            get
            {
                return this.collection[index];
            }
            set
            {
                this.collection[index] = value;
                if (value != null)
                {
                    value.Parent = this.owner;
                    ApplyTransform(value);
                }
            }
        }
    }
}
