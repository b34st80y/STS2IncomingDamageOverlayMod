using Godot;

namespace STS2IncomingDamageOverlayMod.Infrastructure;

internal static class GodotSceneWalker
{
    public static IEnumerable<Node> Walk(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }
}
