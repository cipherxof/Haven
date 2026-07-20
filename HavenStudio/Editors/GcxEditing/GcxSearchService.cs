using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenStudio.Editors.GcxEditing;

public sealed record GcxSearchMatch(GcxScriptNode Node, int Index, int Length);

public sealed class GcxSearchService
{
    private string? _lastQuery;
    private int _lastScriptIndex;
    private int _lastTextIndex;

    public void Reset()
    {
        _lastQuery = null;
        _lastScriptIndex = 0;
        _lastTextIndex = 0;
    }

    public GcxSearchMatch? FindNext(
        string query,
        IEnumerable<GcxScriptNode> scriptNodes,
        Func<GcxScriptNode, string> getSearchText)
    {
        ArgumentNullException.ThrowIfNull(scriptNodes);
        ArgumentNullException.ThrowIfNull(getSearchText);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var nodes = scriptNodes.Where(node => node.Script != null && !node.IsAggregate).ToList();
        if (nodes.Count == 0)
        {
            return null;
        }

        if (!string.Equals(_lastQuery, query, StringComparison.Ordinal))
        {
            _lastQuery = query;
            _lastScriptIndex = 0;
            _lastTextIndex = 0;
        }

        var startScriptIndex = Math.Clamp(_lastScriptIndex, 0, nodes.Count - 1);
        var startTextIndex = Math.Max(0, _lastTextIndex);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = startScriptIndex; index < nodes.Count; index++)
            {
                var node = nodes[index];
                var text = getSearchText(node) ?? string.Empty;
                var searchStart = index == startScriptIndex
                    ? Math.Min(startTextIndex, text.Length)
                    : 0;
                var matchIndex = text.IndexOf(query, searchStart, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    continue;
                }

                _lastScriptIndex = index;
                _lastTextIndex = matchIndex + query.Length;
                return new GcxSearchMatch(node, matchIndex, query.Length);
            }

            startScriptIndex = 0;
            startTextIndex = 0;
        }

        return null;
    }
}
