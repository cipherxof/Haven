using HavenStudio.Editors;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Services.Persistence;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Services;

public sealed class MapPersistenceTests
{
    private const uint EffectHash = 0x445566;
    private static readonly uint ResolvedEffectHash = HavenStudio.Utils.String.HashString(
        EffectHash.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Map_json_round_trip_preserves_untouched_gcx_and_geom_bytes()
    {
        var gcxBytes = BuildGcxFixture();
        var geomBytes = BuildGeomFixture();
        var document = MapDocumentBuilder.Build(
            gcxBytes,
            geomBytes,
            new MapDocumentSources { Gcx = "stage/test.gcx", Geom = "stage/test.geom" },
            Endianness.Big);

        var json = MapJsonIO.Serialize(document);
        var restored = MapJsonIO.Deserialize(json);
        var result = MapDocumentApplier.Apply(restored, Endianness.Big);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"source\": \"pos\"", json);
        Assert.Equal(gcxBytes, result.GcxBytes);
        Assert.Equal(geomBytes, result.GeomBytes);
    }

    [Fact]
    public void Map_json_edits_apply_to_gcx_placements_and_targeted_geom_fields()
    {
        var document = MapDocumentBuilder.Build(
            BuildGcxFixture(),
            BuildGeomFixture(),
            new MapDocumentSources { Gcx = "stage/test.gcx", Geom = "stage/test.geom" },
            Endianness.Big);
        var direct = Assert.Single(document.Placements, placement =>
            placement.Source == MapPlacementSources.Position);
        direct.ModelHash = 0x654321;
        direct.Position = [100, 200, 300];
        direct.DirectionDegrees = [90, 45, -90];
        var effectPlacement = Assert.Single(document.Placements, placement =>
            placement.Source == MapPlacementSources.Effect);
        effectPlacement.Position = [8, 9, 10];

        var effect = Assert.Single(document.Geom.Effects);
        effect.RotationDegrees = [0, 90, 0];
        Assert.Collection(
            document.Geom.CollisionAttributes,
            block => block.Attribute = GeoCollisionAttributes.Water,
            primitive => primitive.Attribute = GeoCollisionAttributes.Player,
            polygon => polygon.Attribute = GeoCollisionAttributes.Missile);

        document = MapJsonIO.Deserialize(MapJsonIO.Serialize(document));
        var result = MapDocumentApplier.Apply(document, Endianness.Big);

        using var gcxStream = new MemoryStream(result.GcxBytes, writable: false);
        var gcx = GcxFile.Read(gcxStream);
        using var geomStream = new MemoryStream(result.GeomBytes, writable: false);
        var geometry = new GeomFile(geomStream, Endianness.Big);
        try
        {
            var placements = new GcxModelReferenceScanner().Scan(
                gcx,
                geometry,
                isMgs3: false).PlacedModels;
            var moved = Assert.Single(placements, placement => placement.ModelHash == 0x654321);
            Assert.Equal(new Vector3(100, 200, 300), moved.Position);
            Assert.Equal(MathF.PI / 2f, moved.Rotation!.Value.X, 5);
            Assert.Equal(MathF.PI / 4f, moved.Rotation.Value.Y, 5);
            Assert.Equal(-MathF.PI / 2f, moved.Rotation.Value.Z, 5);
            var effectMoved = Assert.Single(placements, placement =>
                placement.EffectHash == ResolvedEffectHash);
            Assert.Equal(new Vector3(8, 9, 10), effectMoved.Position);

            var restoredEffect = Assert.Single(geometry.GeoEffects);
            Assert.Equal(MathF.PI / 2f, restoredEffect.RotationY, 5);
            var block = Assert.Single(geometry.GeomBlocks);
            Assert.Equal(GeoCollisionAttributes.Water, block.Attribute);
            var primitive = Assert.Single(geometry.BlockFaceData[block]);
            Assert.Equal(GeoCollisionAttributes.Player, primitive.Attribute);
            Assert.Equal(
                (ushort)GeoCollisionAttributes.Missile,
                Assert.Single(primitive.Poly!).Attribute);
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    [Fact]
    public async Task Map_effect_add_and_delete_are_undoable_without_clearing_prior_history()
    {
        var path = Path.Combine(Path.GetTempPath(), $"haven-effect-history-{Guid.NewGuid():N}.geom");
        await File.WriteAllBytesAsync(path, BuildGeomFixture());
        try
        {
            var host = new SceneHost();
            var collisionEditor = new CollisionEditorViewModel(host);
            using var gcxEditor = new GcxEditorViewModel(host);
            using var mapEditor = new MapEditorViewModel(host, collisionEditor, gcxEditor);
            await collisionEditor.LoadFromFilePathAsync(path);

            var original = Assert.Single(collisionEditor.Effects);
            var originalChunk = collisionEditor.GeomFile!.GeomChunk6.ToArray();
            collisionEditor.SelectedEffect = original;

            mapEditor.AddEffectAtCamera();
            var firstAdded = collisionEditor.Effects[1];
            Assert.Same(firstAdded, collisionEditor.SelectedEffect);
            mapEditor.AddEffectAtCamera();
            Assert.Equal(3, collisionEditor.Effects.Count);
            Assert.Equal(3, mapEditor.Outline[2].Children.Count);
            Assert.Equal("Undo add effect (Ctrl+Z)", mapEditor.UndoToolTip);

            mapEditor.Undo();
            Assert.Equal(2, collisionEditor.Effects.Count);
            Assert.Same(firstAdded, collisionEditor.SelectedEffect);
            Assert.True(mapEditor.CanUndo);
            mapEditor.Undo();
            Assert.Single(collisionEditor.Effects);
            Assert.Same(original, collisionEditor.SelectedEffect);
            Assert.Equal(originalChunk, collisionEditor.GeomFile.GeomChunk6);

            mapEditor.DeleteSelectedEffect();
            Assert.Empty(collisionEditor.Effects);
            Assert.Null(collisionEditor.SelectedEffect);
            Assert.Equal("Undo delete effect (Ctrl+Z)", mapEditor.UndoToolTip);

            mapEditor.Undo();
            Assert.Same(original, Assert.Single(collisionEditor.Effects));
            Assert.Same(original, collisionEditor.SelectedEffect);
            Assert.Equal(originalChunk, collisionEditor.GeomFile.GeomChunk6);
            mapEditor.Redo();
            Assert.Empty(collisionEditor.Effects);
            Assert.Null(collisionEditor.SelectedEffect);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Placement_effect_duplicates_use_the_next_available_hash_and_copy_transform()
    {
        var path = Path.Combine(Path.GetTempPath(), $"haven-effect-duplicate-{Guid.NewGuid():N}.geom");
        await File.WriteAllBytesAsync(path, BuildGeomFixture());
        try
        {
            var host = new SceneHost();
            var collisionEditor = new CollisionEditorViewModel(host);
            await collisionEditor.LoadFromFilePathAsync(path);
            var source = Assert.Single(collisionEditor.Effects);

            var first = collisionEditor.DuplicateEffectForPlacement(source.Effect);
            var second = collisionEditor.DuplicateEffectForPlacement(source.Effect);

            Assert.Equal((ResolvedEffectHash + 1) & 0xFFFFFF, first.Hash);
            Assert.Equal((ResolvedEffectHash + 2) & 0xFFFFFF, second.Hash);
            Assert.Equal(3, collisionEditor.Effects.Count);
            Assert.Equal(source.X, first.Change.Effect.X);
            Assert.Equal(source.Y, first.Change.Effect.Y);
            Assert.Equal(source.Z, first.Change.Effect.Z);
            Assert.Equal(unchecked((int)first.Hash), first.Change.Effect.Effect.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildGcxFixture()
    {
        var direct = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["x"] = 10,
            ["z"] = 30,
            ["y"] = 20,
            ["pitch"] = 0,
            ["roll"] = 0,
            ["yaw"] = 0
        });
        var effect = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x234567u,
            ["eft"] = EffectHash
        });
        var script = new byte[3 + direct.Length + effect.Length + 1];
        script[0] = 0x8E;
        direct.CopyTo(script, 3);
        effect.CopyTo(script, 3 + direct.Length);
        GcxScriptEditor.UpdateProcSize(script);
        var gcx = new Gcx
        {
            Timestamp = 1234,
            CryptoSeed = 0,
            StringSectionPadding = [],
            MainScript = new GcxScript(script)
        };
        using var output = new MemoryStream();
        GcxFile.Write(output, gcx);
        return output.ToArray();
    }

    private static byte[] BuildGeomFixture()
    {
        const int groupOffset = 0x80;
        const int radixOffset = 0xC0;
        const int blockOffset = 0xD0;
        const int primitiveOffset = 0xF0;
        const int refsOffset = 0x120;
        const int propsOffset = 0x190;
        const int propsSize = 0x30;
        const int fileSize = propsOffset + propsSize;
        using var output = new MemoryStream(new byte[fileSize], writable: true);
        using var writer = new EndianBinaryWriter(output, Endianness.Big, leaveOpen: true);

        writer.Write(1u);
        writer.Write((uint)fileSize);
        writer.Write(5);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        WriteChunk(writer, GeoChunkType.GROUPS, refsOffset - groupOffset, groupOffset);
        WriteChunk(writer, GeoChunkType.REFS, 0x70, refsOffset);
        WriteChunk(writer, GeoChunkType.UNKOWN, 0, propsOffset);
        WriteChunk(writer, GeoChunkType.PROPS, propsSize, propsOffset);
        WriteChunk(writer, GeoChunkType.ROUTES, 0, fileSize);
        writer.Write(new byte[8]);
        writer.Write(0x01020304u);
        writer.Write(new byte[0x18]);
        Assert.Equal(groupOffset, output.Position);

        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(1f);
        writer.Write(1);
        writer.Write(1);
        writer.Write(1);
        writer.Write(0x30);
        writer.Write(1);
        writer.Write((short)1);
        writer.Write((short)0x10);
        writer.Write(radixOffset);
        writer.Write(blockOffset);

        output.Position = radixOffset;
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write(new byte[0x0D]);

        output.Position = blockOffset;
        writer.Write((byte)0x10);
        writer.Write((byte)1);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0);
        writer.Write(primitiveOffset);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Floor);

        output.Position = primitiveOffset;
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x10203040u);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Bullet);
        writer.Write(new byte[6]);
        writer.Write((ushort)GeoCollisionAttributes.Bullet);
        writer.Write(new byte[8]);

        output.Position = refsOffset;
        writer.Write(new byte[0x70]);

        output.Position = propsOffset;
        writer.Write(0);
        writer.Write(0);
        writer.Write(unchecked((int)ResolvedEffectHash));
        writer.Write(2 | (4 << 10));
        writer.Write(5f);
        writer.Write(6f);
        writer.Write(7f);
        writer.Write(1f);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(new byte[10]);
        writer.Flush();
        return output.ToArray();
    }

    private static void WriteChunk(
        EndianBinaryWriter writer,
        GeoChunkType type,
        int size,
        int offset)
    {
        writer.Write((ushort)type);
        writer.Write((ushort)0);
        writer.Write(size);
        writer.Write(offset);
    }
}
