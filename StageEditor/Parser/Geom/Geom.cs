﻿using Haven.Parser.Geom.Prim;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom
{
    public class Geom
    {
        public static List<Geom> GeomList = new List<Geom>();

        public enum Primitive
        {
            GEO_DOT = 0, // 0x20
            GEO_LINE = 1, // 0x30
            GEO_POLY = 2, // 0x10
            GEO_BOX = 3, // 0x30
            GEO_FIELD = 4, // 0x20
            GEO_REF = 5, // 0x70
            GEO_UNKNOWN = 6
        }

        public byte Length;
        public byte Type;
        public byte Field002;
        public byte Field003;
        public int Next;
        public int Prev;
        public int Child;
        public uint Name;
        public int Field014;
        public ulong Attribute;
        public byte[] Data;

        public uint Flag;

        public GeoPrimDot[]? Dot;
        public GeoPrimLine[]? Line;
        public GeoPrimField[]? Field;
        public GeoPrimBox[]? Box;
        public GeoPrimPoly[]? Poly;

        public Geom(BinaryReader reader)
        {
            var pos = reader.BaseStream.Position;

            Flag = reader.ReadUInt32();
            var bytes = BitConverter.GetBytes(Flag);
            Length = bytes[3];
            Type = bytes[2];
            Field002 = bytes[1];
            Field003 = bytes[0];
            Next = reader.ReadInt32();
            Prev = reader.ReadInt32();
            Child = reader.ReadInt32();
            Name = reader.ReadUInt32();
            Field014 = reader.ReadInt32();
            Attribute = reader.ReadUInt64();
            Data = new byte[0];

            DictionaryFile.GetHashString(Name);

            Primitive primType = GetPrimType();

            switch (primType)
            {
                case Primitive.GEO_DOT:
                    Dot = new GeoPrimDot[Length];

                    for (int i = 0; i < Length; i++)
                    {
                        Dot[i] = new GeoPrimDot(reader);
                    }
                    break;
                case Primitive.GEO_LINE:
                    Line = new GeoPrimLine[Length];

                    for (int i = 0; i < Length; i++)
                    {
                        Line[i] = new GeoPrimLine(reader);
                    }
                    break;
                case Primitive.GEO_POLY:
                    Poly = new GeoPrimPoly[Length];

                    for (int i = 0; i < Length; i++)
                    {
                        Poly[i] = new GeoPrimPoly(reader);
                    }

                    if (Length % 2 == 1)
                    {
                        Data = reader.ReadBytes(8);
                    }
                    break;
                case Primitive.GEO_BOX:
                    Box = new GeoPrimBox[Length];

                    for (int i = 0; i < Length; i++)
                    {
                        Box[i] = new GeoPrimBox(reader);
                    }

                    if (Field003 == 0x23)
                    {
                        Data = reader.ReadBytes(0x10);
                    }
                    break;
                case Primitive.GEO_FIELD:
                    Field = new GeoPrimField[1];
                    Field[0] = new GeoPrimField(reader);

                    if (Type != 0 && Field003 == 0x24)
                    {
                        Data = reader.ReadBytes(0x10);
                    }
                    break;
                case Primitive.GEO_REF:
                    Data = reader.ReadBytes(0x70 * Length);
                    break;
                case Primitive.GEO_UNKNOWN:
                    break;
                default:
                    Debug.WriteLine("Unknown primitive type {0:X} field={1:X}, offset={2:X}", primType, Field003, pos);
                    break;
            }

            GeomList.Add(this);
        }

        public int GetSize()
        {
            var primType = GetPrimType();
            var primTypeSize = new int[] { 0x20, 0x30, 0x08, 0x30, 0x20, 0x70, 0x00 };
            return primTypeSize[(int)primType] + Data.Length;
        }

        public Primitive GetPrimType()
        {
            uint primType = Flag & 0x7;

            if (primType >= 0 && primType <= 5)
                return (Primitive)primType;

            return Primitive.GEO_UNKNOWN;
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Length);
            writer.Write(Type);
            writer.Write(Field002);
            writer.Write(Field003);
            writer.Write(Next);
            writer.Write(Prev);
            writer.Write(Child);
            writer.Write(Name);
            writer.Write(Field014);
            writer.Write(Attribute);

            Primitive primType = GetPrimType();

            switch (primType)
            {
                case Primitive.GEO_DOT:
                    for (int i = 0; i < Length; i++)
                    {
                        Dot?[i].WriteTo(writer);
                    }
                    break;
                case Primitive.GEO_LINE:
                    for (int i = 0; i < Length; i++)
                    {
                        Line?[i].WriteTo(writer);
                    }
                    break;
                case Primitive.GEO_POLY:
                    for (int i = 0; i < Length; i++)
                    {
                        Poly?[i].WriteTo(writer);
                    }
                    break;
                case Primitive.GEO_BOX:
                    for (int i = 0; i < Length; i++)
                    {
                        Box?[i].WriteTo(writer);
                    }
                    break;
                case Primitive.GEO_FIELD:
                    Field?[0].WriteTo(writer);
                    break;
                default:
                    break;
            }

            writer.Write(Data);
        }
    }
}
