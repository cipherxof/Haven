using System;
using System.ComponentModel;
using Avalonia3DControl.Core.Models;
using HavenStudio.Editors.GcxEditing;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public sealed record SpectatorCameraEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Func<SpectatorCameraEntity, Vector3, Vector3, string?> _edit;
    public GcxCameraReference Source { get; }
    public Model3D Marker { get; } = new();
    public Vector3 Position { get; private set; }
    public Vector3 Target { get; private set; }
    public string EditStatus { get; private set; } = string.Empty;
    public string SourceText => $"GCX {Source.ScriptName}, table {Source.TableIndex+1}, row {Source.RowIndex+1}. Extra value {Source.Extra} is preserved." +
        (GcxCameraWriter.Scan(Source.Script.Bytes)[Source.TableIndex].NeedsLengthRepair ? " This table has an incorrect byte count; editing a camera will repair it." : "");
    public SpectatorCameraEntity(GcxCameraReference source,
        Func<SpectatorCameraEntity, Vector3, Vector3, string?> edit)
        : base($"Camera {source.RowIndex+1:00} ({source.ScriptName}, table {source.TableIndex+1})")
    {
        Source = source; _edit = edit;
        Refresh();
    }
    public float PositionX { get => Position.X; set => Edit(new(value,Position.Y,Position.Z), Target + new Vector3(value-Position.X,0,0)); }
    public float PositionY { get => Position.Y; set => Edit(new(Position.X,value,Position.Z), Target + new Vector3(0,value-Position.Y,0)); }
    public float PositionZ { get => Position.Z; set => Edit(new(Position.X,Position.Y,value), Target + new Vector3(0,0,value-Position.Z)); }
    public float TargetX { get => Target.X; set => Edit(Position, new(value,Target.Y,Target.Z)); }
    public float TargetY { get => Target.Y; set => Edit(Position, new(Target.X,value,Target.Z)); }
    public float TargetZ { get => Target.Z; set => Edit(Position, new(Target.X,Target.Y,value)); }
    public float Yaw { get => MathF.Atan2(Target.X-Position.X,Target.Z-Position.Z)*180/MathF.PI; set => Rotate(value,Pitch); }
    public float Pitch { get => MathF.Atan2(Target.Y-Position.Y, (Target-Position).Xz.Length)*180/MathF.PI; set => Rotate(Yaw,value); }
    private void Rotate(float yaw, float pitch)
    {
        if (!float.IsFinite(yaw) || !float.IsFinite(pitch) || pitch < -90 || pitch > 90)
        { EditStatus = "Yaw must be finite; pitch must be between -90 and 90 degrees."; Changed(); return; }
        float y = yaw*MathF.PI/180, p = pitch*MathF.PI/180;
        var direction = new Vector3(MathF.Sin(y)*MathF.Cos(p), MathF.Sin(p), MathF.Cos(y)*MathF.Cos(p));
        Edit(Position, Position + direction*MathF.Max(1,(Target-Position).Length));
    }
    private void Edit(Vector3 eye, Vector3 target)
    {
        EditStatus = _edit(this,eye,target) ?? string.Empty;
        Changed();
    }
    public void Refresh()
    {
        var tables = GcxCameraWriter.Scan(Source.Script.Bytes);
        var site = tables[Source.TableIndex];
        Position = GcxCameraWriter.Vector(site,Source.RowIndex*7);
        Target = GcxCameraWriter.Vector(site,Source.RowIndex*7+3);
        UpdateMarker(); Changed();
    }
    private void UpdateMarker()
    {
        // Same pointed cube silhouette as Effects; +Z is the look-at direction.
        float[] points = [ -.5f,-.5f,-.5f, .5f,-.5f,-.5f, .5f,.5f,-.5f, -.5f,.5f,-.5f,
            -.5f,-.5f,.5f, .5f,-.5f,.5f, .5f,.5f,.5f, -.5f,.5f,.5f,
            -.28f,-.35f,.5f, .28f,-.35f,.5f, 0,-.35f,1.25f,
            -.28f,.35f,.5f, .28f,.35f,.5f, 0,.35f,1.25f ];
        var forward = Target-Position;
        forward = forward.LengthSquared < .001f ? Vector3.UnitZ : forward.Normalized();
        var right = Vector3.Cross(MathF.Abs(forward.Y) > .999f ? Vector3.UnitZ : Vector3.UnitY,forward).Normalized();
        var up = Vector3.Cross(forward,right);
        for (int i=0;i<points.Length;i+=3)
        {
            var point = 300*(right*points[i]+up*points[i+1]+forward*points[i+2]);
            points[i]=point.X; points[i+1]=point.Y; points[i+2]=point.Z;
        }
        Marker.Name = DisplayName; Marker.Position = Position; Marker.Color = new(1,.08f,.08f);
        Marker.Alpha = 1; Marker.MaterialIndex = -1; Marker.Scale = Vector3.One;
        Marker.RenderAfterTransparent = true;
        Marker.Positions = points;
        Marker.Indices = [0,1,2,2,3,0,4,5,6,6,7,4,0,1,5,5,4,0,2,3,7,7,6,2,0,3,7,7,4,0,1,2,6,6,5,1,
            8,10,9,11,12,13,8,9,12,12,11,8,9,10,13,13,12,9,10,8,11,11,13,10];
        Marker.VertexCount = points.Length/3; Marker.IndexCount = Marker.Indices.Length;
        Marker.VerticesNeedUpdate = true; Marker.IndicesNeedUpdate = true;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
}
