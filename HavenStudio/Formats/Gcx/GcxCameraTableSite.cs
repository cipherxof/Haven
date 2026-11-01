using System.Collections.Generic;
using System;
using System.Buffers.Binary;

namespace HavenStudio.Formats.Gcx;

/// <summary>Literal preset camera table in the C2C7A0 actor's 2647DA parameter.
/// Each row is eye XYZ, look-at XYZ, and an uninterpreted seventh value.</summary>
public sealed record GcxCameraTableSite(int CommandOffset, int CommandLength,
    int ParameterOffset, int ParameterLength, IReadOnlyList<GcxLiteralSite> Literals,
    int DeclaredParameterLength = 0)
{
    public int Count => Literals.Count / 7;
    public bool NeedsLengthRepair => DeclaredParameterLength != 0 && DeclaredParameterLength != ParameterLength;

    public static GcxCameraTableSite? Read(byte[] bytes, int commandOffset, int commandLength,
        int parameterOffset, int declaredLength, int payloadOffset)
    {
        int end = commandOffset+commandLength-1;
        if(end >= bytes.Length || bytes[end] != 0 || payloadOffset+4 >= end) return null;
        var literals=new List<GcxLiteralSite>();
        for(int offset=payloadOffset+4;offset<end;)
        {
            byte tag=bytes[offset]; int width,value; GcxLiteralEncoding encoding;
            if(tag>=0xC0) { width=1;value=(tag&63)-1;encoding=GcxLiteralEncoding.PackedNumber; }
            else if(tag==1 && offset+3<=end) { width=3;value=BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset+1,2));encoding=GcxLiteralEncoding.Int16; }
            else if(tag==2 && offset+2<=end) { width=2;value=bytes[offset+1];encoding=GcxLiteralEncoding.UInt8; }
            else if(tag==8 && offset+3<=end) { width=3;value=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset+1,2));encoding=GcxLiteralEncoding.UInt16; }
            else if(tag==9 && offset+5<=end) { width=5;value=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset+1,4));encoding=GcxLiteralEncoding.Int32; }
            else return null;
            literals.Add(new(offset,width,encoding,value)); offset+=width;
        }
        // Some custom JJ files retained the donor parameter length after expanding
        // coordinates. Recover only a complete final literal table inside an intact
        // command; never scan across its terminator or infer missing camera values.
        if(literals.Count==0 || literals.Count%7!=0 || parameterOffset+declaredLength>end) return null;
        return new(commandOffset,commandLength,parameterOffset,end-parameterOffset,literals,declaredLength);
    }
}
