using System;
using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public class GeoVertexHeader
{
    public int Length;
    public int VertexStart;
    public int FaceStart;
    public int PositionStart;
    public Vector4[] Data;

    public GeoVertexHeader(
        Vector4[] data,
        int vertexStart = 2,
        int faceStart = 1,
        int positionStart = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        Length = data.Length;
        VertexStart = vertexStart;
        FaceStart = faceStart;
        PositionStart = positionStart;
        Data = data;
    }

    public GeoVertexHeader(EndianBinaryReader reader)
    {
        Length = reader.ReadInt32();
        VertexStart = reader.ReadInt32();
        FaceStart = reader.ReadInt32();
        PositionStart = reader.ReadInt32();
        Data = new Vector4[Length];

        for (int i = 0; i < Length; i++)
        {
            Data[i] = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Length);
        writer.Write(VertexStart);
        writer.Write(FaceStart);
        writer.Write(PositionStart);
        for (int i = 0; i < Data.Length; i++)
        {
            writer.Write(Data[i].X);
            writer.Write(Data[i].Y);
            writer.Write(Data[i].Z);
            writer.Write(Data[i].W);
        }
    }
}
