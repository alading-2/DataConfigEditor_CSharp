namespace DataConfigEditor.Workspace;

public static class WorkspaceLayout
{
    public static int GetSafePanel2MinSize(int totalWidth, int panel1MinSize, int desiredPanel2MinSize)
    {
        var available = Math.Max(0, totalWidth - panel1MinSize);
        return Math.Min(desiredPanel2MinSize, available);
    }

    public static int GetSafeSplitterDistance(
        int totalWidth,
        int panel1MinSize,
        int panel2MinSize,
        int desiredDistance)
    {
        var maxDistance = totalWidth - panel2MinSize;
        if (maxDistance < panel1MinSize)
            return panel1MinSize;

        return Math.Clamp(desiredDistance, panel1MinSize, maxDistance);
    }
}
