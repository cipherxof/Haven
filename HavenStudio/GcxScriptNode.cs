using HavenStudio.Formats.Gcx;

namespace HavenStudio;

public sealed class GcxScriptNode
{
    public GcxScriptNode(string name, GcxScript script)
    {
        Name = name;
        Script = script;
        IsAggregate = false;
    }

    public string Name { get; }
    public GcxScript? Script { get; }
    public bool IsAggregate { get; }

    public static GcxScriptNode CreateAggregate(string name)
    {
        return new GcxScriptNode(name, script: null, isAggregate: true);
    }

    private GcxScriptNode(string name, GcxScript? script, bool isAggregate)
    {
        Name = name;
        Script = script;
        IsAggregate = isAggregate;
    }
}
