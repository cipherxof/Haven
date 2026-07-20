using System.Collections.Generic;
using System.IO;

namespace HavenStudio.Formats.Mdn;

    public sealed class MdnVertexElement
    {
        public int Type { get; }
        public int Format { get; set; }
        
        public MdnVertexElement? Clone { get; set; }

        // Backing storage (only used when Clone == null).
        private object _data = new List<byte>(); // placeholder; we swap based on format.

        public MdnVertexElement(int type) => Type = type;

        public void SplitFromClone()
        {
            if (Clone is null) return;

            // Copy the underlying list (shallow copy of primitives).
            _data = Clone.GetDataObject() switch
            {
                List<float> lf => new List<float>(lf),
                List<short> ls => new List<short>(ls),
                List<byte> lb => new List<byte>(lb),
                List<int> li => new List<int>(li),
                _ => throw new InvalidDataException("Unsupported vertex element data type.")
            };
            Clone = null;
        }

        public object GetDataObject() => Clone?.GetDataObject() ?? _data;

        public List<float> GetFloatData()
        {
            if (Clone != null) return Clone.GetFloatData();
            if (_data is not List<float> lf) _data = lf = new List<float>();
            return lf;
        }

        public List<short> GetShortData()
        {
            if (Clone != null) return Clone.GetShortData();
            if (_data is not List<short> ls) _data = ls = new List<short>();
            return ls;
        }

        public List<byte> GetByteData()
        {
            if (Clone != null) return Clone.GetByteData();
            if (_data is not List<byte> lb) _data = lb = new List<byte>();
            return lb;
        }

        public List<int> GetIntData()
        {
            if (Clone != null) return Clone.GetIntData();
            if (_data is not List<int> li) _data = li = new List<int>();
            return li;
        }
    }