namespace Rql.Tests;

/// <summary>A Random that lands the faces it's given, in order, so a test can say exactly what was rolled.</summary>
internal sealed class Faces(params int[] faces) : Random
{
    private int next;

    /// <summary>How many faces have been rolled, to check nothing extra was.</summary>
    public int Used => next;

    public override int Next(int maxValue)
    {
        if (next >= faces.Length)
        {
            throw new InvalidOperationException($"rolled more dice than the {faces.Length} faces given");
        }
        int face = faces[next++];
        // Fate dice ask for 0 to 2 and take 1 off; every other die asks for 0 to sides - 1 and adds 1.
        return Fate ? face + 1 : face - 1;
    }

    public bool Fate { get; init; }
}
