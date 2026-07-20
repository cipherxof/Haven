using System;
using System.Collections.Generic;

namespace HavenStudio.Utils;

public static class TreeTraversal
{
    public static IEnumerable<T> Flatten<T>(
        IEnumerable<T> roots,
        Func<T, IEnumerable<T>> getChildren)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var descendant in Flatten(getChildren(root), getChildren))
            {
                yield return descendant;
            }
        }
    }
}
