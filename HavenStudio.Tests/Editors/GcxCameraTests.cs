using System.Buffers.Binary;
using HavenStudio.Editors;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Gcx;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public class GcxCameraTests
{
    private static byte[] Fixture(bool compact=false, int rows=10)
    {
        var values=Enumerable.Range(0,rows).SelectMany(i=>new[]{100+i,200+i,300+i,400+i,500+i,600+i,200});
        var literals=values.SelectMany((v,i)=>i%7==6 ? new byte[]{2,200} : compact ? new byte[]{1,(byte)v,(byte)(v>>8)} : GcxCommandBuilder.Int32LiteralBytes(v));
        var param=GcxCommandBuilder.WrapTaggedPayload(0x50,new byte[]{(byte)'p',0xDA,0x47,0x26}.Concat(literals).ToArray());
        var command=GcxCommandBuilder.WrapTaggedPayload(0x60,Convert.FromHexString("A792650806A0C7C2069A9A1E").Concat(param).Append((byte)0).ToArray());
        // Extended procedure prefix allows promotion of literals without moving earlier commands.
        var result=new byte[3+command.Length+4]; result[0]=0x8E;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(1,2),(ushort)(command.Length+1));
        command.CopyTo(result,3); return result;
    }
    [Fact] public void Discovers_all_ten_rows_and_preserves_noop_exactly()
    {
        var bytes=Fixture();var table=Assert.Single(GcxCameraWriter.Scan(bytes)); Assert.Equal(10,table.Count);
        Assert.Equal(new Vector3(100,200,300),GcxCameraWriter.Vector(table,0));
        Assert.Equal(bytes,GcxCameraWriter.Write(bytes,0,0,new(100,200,300),new(400,500,600)));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Changes_only_requested_row_and_preserves_extra(bool compact)
    {
        var before=Fixture(compact);var old=Assert.Single(GcxCameraWriter.Scan(before));
        var after=GcxCameraWriter.Write(before,0,4,new(-96000,3975,-240000),new(-90000,6000,-230000));
        var table=Assert.Single(GcxCameraWriter.Scan(after)); Assert.Equal(10,table.Count);
        Assert.Equal(new Vector3(-96000,3975,-240000),GcxCameraWriter.Vector(table,28));
        Assert.Equal(new Vector3(-90000,6000,-230000),GcxCameraWriter.Vector(table,31));
        for(int i=0;i<70;i++) if(i<28 || i>=34) Assert.Equal(old.Literals[i].Value,table.Literals[i].Value);
        Assert.Equal(before[^3..],after[^3..]);
        if(!compact)
        {
            var allowed=old.Literals.Skip(28).Take(6).SelectMany(l=>Enumerable.Range(l.Offset,l.Width)).ToHashSet();
            Assert.All(Enumerable.Range(0,before.Length).Where(i=>!allowed.Contains(i)),i=>Assert.Equal(before[i],after[i]));
        }
        Assert.Equal(before,Fixture(compact));
    }
    [Fact] public void Rejects_invalid_edits()
    {
        var b=Fixture();
        Assert.Throws<InvalidDataException>(()=>GcxCameraWriter.Write(b,0,10,Vector3.Zero,Vector3.One));
        Assert.Throws<InvalidDataException>(()=>GcxCameraWriter.Write(b,0,0,new(float.NaN,0,0),Vector3.One));
        Assert.Throws<InvalidDataException>(()=>GcxCameraWriter.Write(b,0,0,Vector3.One,Vector3.One));
        Assert.Throws<InvalidDataException>(()=>GcxCameraWriter.Write(b,0,0,Vector3.Zero,new(.1f,.1f,.1f)));
    }
    [Fact] public void Repairs_JJ_short_parameter_length_only_on_edit()
    {
        var original=Fixture(true);var table=Assert.Single(GcxCameraWriter.Scan(original));
        var b=original.ToArray(); b[table.ParameterOffset+1]-=4;
        var damaged=Assert.Single(GcxCameraWriter.Scan(b));Assert.True(damaged.NeedsLengthRepair);Assert.Equal(10,damaged.Count);
        Assert.Equal(b,GcxCameraWriter.Write(b,0,0,new(100,200,300),new(400,500,600)));
        var edited=GcxCameraWriter.Write(b,0,0,new(-96000,3975,-240000),new(-90000,6000,-230000));
        var repaired=Assert.Single(GcxCameraWriter.Scan(edited)); Assert.False(repaired.NeedsLengthRepair);
        Assert.Equal(10,repaired.Count);
        for(int i=6;i<70;i++) Assert.Equal(table.Literals[i].Value,repaired.Literals[i].Value);
    }
    [Fact] public void Wrong_actor_and_incomplete_rows_are_not_exposed()
    {
        var b=Fixture(); int at=b.AsSpan().IndexOf(Convert.FromHexString("A0C7C2")); b[at]=0;
        Assert.Empty(GcxCameraWriter.Scan(b));
        Assert.Empty(GcxCameraWriter.Scan(Fixture()[..30]));
    }
    [Fact] public void Entity_translation_and_rotation_keep_marker_and_source_in_sync()
    {
        var script=new GcxScript(Fixture());
        var reference=new GcxCameraReference(script,"main",0,0,new(100,200,300),new(400,500,600),200);
        var entity=new SpectatorCameraEntity(reference,(e,eye,target)=>
        { script.Bytes=GcxCameraWriter.Write(script.Bytes,0,0,eye,target); e.Refresh(); return null; });
        var direction=entity.Target-entity.Position;
        entity.PositionX=-50000; Assert.Equal(direction,entity.Target-entity.Position);
        Assert.Equal(entity.Position,entity.Marker.Position);
        entity.Yaw=90; entity.Pitch=0;
        Assert.InRange(MathF.Abs(entity.Yaw-90),0,.2f);Assert.InRange(MathF.Abs(entity.Pitch),0,.2f);
        Assert.True(entity.Target.X>entity.Position.X);
        var old=script.Bytes;entity.Pitch=100;Assert.Same(old,script.Bytes);
    }
}
