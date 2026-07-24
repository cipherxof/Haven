using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

    public sealed class MdnVertexBuffer
    {
        public const int TypePositions = 0x0;
        public const int TypeJointWeights = 0x1;
        public const int TypeNormals = 0x2;
        public const int TypeColors = 0x3;
        public const int TypeJointIndices = 0x7;
        public const int TypeTextureCoords0 = 0x8;
        public const int TypeTextureCoords1 = 0x9;
        public const int TypeTextureCoords2 = 0xA;
        public const int TypeTextureCoords3 = 0xB;
        public const int TypeTextureCoords4 = 0xC;
        public const int TypeTextureCoords5 = 0xD;
        public const int TypeTangents = 0xE;

        public const int FormatFloat = 0x1;
        public const int FormatHalfFloat = 0x7;
        public const int FormatByte8 = 0x8;
        public const int FormatByte9 = 0x9;
        public const int FormatDecTri = 0xA;

        private readonly MdnVertexElement _positions = new(TypePositions);
        private readonly MdnVertexElement _jointWeights = new(TypeJointWeights);
        private readonly MdnVertexElement _normals = new(TypeNormals);
        private readonly MdnVertexElement _colors = new(TypeColors);
        private readonly MdnVertexElement _jointIndices = new(TypeJointIndices);
        private readonly MdnVertexElement _tex0 = new(TypeTextureCoords0);
        private readonly MdnVertexElement _tex1 = new(TypeTextureCoords1);
        private readonly MdnVertexElement _tex2 = new(TypeTextureCoords2);
        private readonly MdnVertexElement _tex3 = new(TypeTextureCoords3);
        private readonly MdnVertexElement _tex4 = new(TypeTextureCoords4);
        private readonly MdnVertexElement _tex5 = new(TypeTextureCoords5);
        private readonly MdnVertexElement _tangents = new(TypeTangents);

        public List<MdnVertexElement> Elements { get; } = new();

        public MdnVertexElement GetElementByType(int type) => type switch
        {
            TypePositions => _positions,
            TypeJointWeights => _jointWeights,
            TypeNormals => _normals,
            TypeColors => _colors,
            TypeJointIndices => _jointIndices,
            TypeTextureCoords0 => _tex0,
            TypeTextureCoords1 => _tex1,
            TypeTextureCoords2 => _tex2,
            TypeTextureCoords3 => _tex3,
            TypeTextureCoords4 => _tex4,
            TypeTextureCoords5 => _tex5,
            TypeTangents => _tangents,
            _ => GetOrCreateExtraElement(type)
        };

        /// <summary>
        /// Engine build 2739 bakes FOUR per-vertex streams (three normalised
        /// colour streams plus a log2-quantised HDR scale stream). Their MDN
        /// element types are still being located; unknown types are captured
        /// here instead of throwing, so the stream inventory can log them.
        /// </summary>
        private readonly Dictionary<int, MdnVertexElement> _extraElements = new();

        public IReadOnlyDictionary<int, MdnVertexElement> ExtraElements => _extraElements;

        private MdnVertexElement GetOrCreateExtraElement(int type)
        {
            if (!_extraElements.TryGetValue(type, out var element))
            {
                element = new MdnVertexElement(type);
                _extraElements[type] = element;
            }
            return element;
        }
        
        public static MdnVertexBuffer ReadDefinitionFrom(EndianBinaryReader r)
        {
            var vb = new MdnVertexBuffer();

            r.Skip(4);

            int definitionCount = r.ReadInt32();
            int definitionSize = r.ReadInt32();
            int definitionStart = r.ReadInt32();
            _ = definitionSize;
            _ = definitionStart;

            var defs = new byte[definitionCount];
            for (int i = 0; i < definitionCount; i++)
                defs[i] = r.ReadByte();

            r.Align(0x10);

            var positions = new byte[definitionCount];
            for (int i = 0; i < definitionCount; i++)
                positions[i] = r.ReadByte();

            r.Align(0x10);

            for (int j = 0; j < definitionCount; j++)
            {
                int format = (defs[j] >> 4) & 0xF;
                int type = defs[j] & 0xF;

                var element = vb.GetElementByType(type);
                element.Format = format;

                int dupIndex = -1;
                for (int k = 0; k < j; k++)
                {
                    if (positions[k] == positions[j]) { dupIndex = k; break; }
                }

                if (dupIndex >= 0)
                {
                    int dupType = defs[dupIndex] & 0xF;
                    element.Clone = vb.GetElementByType(dupType);
                }

                vb.Elements.Add(element);
            }

            return vb;
        }

        public void WriteDefinitionTo(EndianBinaryWriter w)
        {
            w.WriteZero(4);
            w.WriteInt32(Elements.Count);

            long sizePos = w.BaseStream.Position;
            w.WriteZero(0x8); 

            foreach (var element in Elements)
            {
                int def = ((element.Format & 0xF) << 4) | (element.Type & 0xF);
                w.Write((byte)def);
            }

            w.Align(0x10);

            int offset = 0;
            foreach (var element in Elements)
            {
                int size = element.Format switch
                {
                    FormatFloat => 0xC,
                    FormatHalfFloat => 0x4,
                    FormatByte8 => 0x4,
                    FormatByte9 => 0x4,
                    FormatDecTri => 0x4,
                    _ => throw new InvalidDataException($"Write unknown def format: 0x{element.Format:X2}")
                };

                if (element.Clone != null)
                    offset -= size;

                w.Write((byte)offset);
                offset += size;
            }

            w.Align(0x10);

            long save = w.BaseStream.Position;
            w.BaseStream.Position = sizePos;
            w.WriteInt32(offset); // definitionSize
            w.BaseStream.Position = save;
        }

        public void ReadVertexDataFrom(EndianBinaryReader r, int vertexCount)
        {
            for (int n = 0; n < vertexCount; n++)
            {
                foreach (var element in Elements)
                {
                    if (element.Clone != null)
                        continue;

                    switch (element.Format)
                    {
                        case FormatFloat:
                            element.GetFloatData().Add(r.ReadSingle());
                            element.GetFloatData().Add(r.ReadSingle());
                            element.GetFloatData().Add(r.ReadSingle());
                            break;
                        case FormatHalfFloat:
                            element.GetShortData().Add(r.ReadInt16());
                            element.GetShortData().Add(r.ReadInt16());
                            break;
                        case FormatByte8:
                        case FormatByte9:
                            element.GetByteData().Add(r.ReadByte());
                            element.GetByteData().Add(r.ReadByte());
                            element.GetByteData().Add(r.ReadByte());
                            element.GetByteData().Add(r.ReadByte());
                            break;
                        case FormatDecTri:
                            element.GetIntData().Add(r.ReadInt32());
                            break;
                        default:
                            throw new InvalidDataException($"Read unknown def format: 0x{element.Format:X2}");
                    }
                }
            }
        }

        public void WriteVertexDataTo(EndianBinaryWriter w, int vertexCount)
        {
            var cursors = Elements.Select(_ => 0).ToArray();

            for (int n = 0; n < vertexCount; n++)
            {
                for (int j = 0; j < Elements.Count; j++)
                {
                    var element = Elements[j];
                    if (element.Clone != null) continue;

                    switch (element.Format)
                    {
                        case FormatFloat:
                        {
                            var list = element.GetFloatData();
                            int c = cursors[j];
                            w.WriteSingle(list[c + 0]);
                            w.WriteSingle(list[c + 1]);
                            w.WriteSingle(list[c + 2]);
                            cursors[j] = c + 3;
                            break;
                        }
                        case FormatHalfFloat:
                        {
                            var list = element.GetShortData();
                            int c = cursors[j];
                            w.WriteInt16(list[c + 0]);
                            w.WriteInt16(list[c + 1]);
                            cursors[j] = c + 2;
                            break;
                        }
                        case FormatByte8:
                        case FormatByte9:
                        {
                            var list = element.GetByteData();
                            int c = cursors[j];
                            w.Write(list[c + 0]);
                            w.Write(list[c + 1]);
                            w.Write(list[c + 2]);
                            w.Write(list[c + 3]);
                            cursors[j] = c + 4;
                            break;
                        }
                        case FormatDecTri:
                        {
                            var list = element.GetIntData();
                            int c = cursors[j];
                            w.WriteInt32(list[c]);
                            cursors[j] = c + 1;
                            break;
                        }
                        default:
                            throw new InvalidDataException($"Write unknown def format: 0x{element.Format:X2}");
                    }
                }
            }
        }
    }