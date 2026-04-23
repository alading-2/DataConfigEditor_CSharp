using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceLayoutTests
{
    [Theory]
    [InlineData(1400, 220, 300, 300)]
    [InlineData(500, 220, 300, 280)]
    public void GetSafePanel2MinSize_ClampsToAvailableWidth(
        int totalWidth,
        int panel1MinSize,
        int desiredPanel2MinSize,
        int expected)
    {
        var result = WorkspaceLayout.GetSafePanel2MinSize(
            totalWidth,
            panel1MinSize,
            desiredPanel2MinSize);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1400, 220, 300, 340, 340)]
    [InlineData(500, 220, 300, 340, 220)]
    [InlineData(900, 220, 300, 100, 220)]
    public void GetSafeSplitterDistance_ReturnsValueWithinLegalRange(
        int totalWidth,
        int panel1MinSize,
        int panel2MinSize,
        int desiredDistance,
        int expected)
    {
        var result = WorkspaceLayout.GetSafeSplitterDistance(
            totalWidth,
            panel1MinSize,
            panel2MinSize,
            desiredDistance);

        Assert.Equal(expected, result);
    }
}
