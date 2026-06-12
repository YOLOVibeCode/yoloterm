namespace YOLOTerm.Core.Layout;

/// <summary>
/// Pure function: (pane set, preset, container size, drag deltas) → pane rects
/// No UI types. Fixture-tested behavior.
/// </summary>
public class LayoutEngine
{
    /// <summary>
    /// Calculate layout for given panes, preset, and container size
    /// </summary>
    public List<PaneRect> Calculate(
        List<string> paneIds,
        LayoutPreset preset,
        ContainerSize containerSize,
        List<DragDelta>? dragDeltas = null,
        string? zoomedPane = null)
    {
        dragDeltas ??= new List<DragDelta>();

        // If a pane is zoomed, it fills the entire container
        if (zoomedPane != null)
        {
            return new List<PaneRect>
            {
                new PaneRect(
                    id: zoomedPane,
                    x: 0,
                    y: 0,
                    width: containerSize.Width,
                    height: containerSize.Height)
            };
        }

        int count = paneIds.Count;
        if (count == 0) return new List<PaneRect>();

        // Single pane always fills container
        if (count == 1)
        {
            return new List<PaneRect>
            {
                new PaneRect(
                    id: paneIds[0],
                    x: 0,
                    y: 0,
                    width: containerSize.Width,
                    height: containerSize.Height)
            };
        }

        // Apply preset
        var actualPreset = preset == LayoutPreset.Auto ? AutoPreset(count) : preset;

        return actualPreset switch
        {
            LayoutPreset.Single => LayoutSingle(paneIds, containerSize),
            LayoutPreset.Columns => LayoutColumns(paneIds, containerSize),
            LayoutPreset.Rows => LayoutRows(paneIds, containerSize),
            LayoutPreset.Grid => LayoutGrid(paneIds, containerSize),
            LayoutPreset.MainLeft => LayoutMainLeft(paneIds, containerSize),
            LayoutPreset.MainRight => LayoutMainRight(paneIds, containerSize),
            LayoutPreset.Auto => LayoutAuto(paneIds, containerSize),
            _ => throw new ArgumentException($"Unknown preset: {actualPreset}")
        };
    }

    /// <summary>
    /// Equalize all panes (reset custom sizing)
    /// </summary>
    public List<PaneRect> Equalize(
        List<string> paneIds,
        LayoutPreset preset,
        ContainerSize containerSize)
    {
        // Equalize just recalculates without drag deltas
        return Calculate(paneIds, preset, containerSize, new List<DragDelta>());
    }

    // MARK: - Private Layout Algorithms

    private LayoutPreset AutoPreset(int count)
    {
        return count switch
        {
            1 => LayoutPreset.Single,
            2 => LayoutPreset.Columns,
            3 => LayoutPreset.MainLeft,
            4 => LayoutPreset.Grid,
            _ => LayoutPreset.Grid // 5+ panes: use grid
        };
    }

    private List<PaneRect> LayoutSingle(List<string> paneIds, ContainerSize containerSize)
    {
        if (paneIds.Count == 0) return new List<PaneRect>();

        return new List<PaneRect>
        {
            new PaneRect(
                id: paneIds[0],
                x: 0,
                y: 0,
                width: containerSize.Width,
                height: containerSize.Height)
        };
    }

    private List<PaneRect> LayoutColumns(List<string> paneIds, ContainerSize containerSize)
    {
        int count = paneIds.Count;
        double colWidth = containerSize.Width / count;

        var rects = new List<PaneRect>();
        for (int i = 0; i < count; i++)
        {
            rects.Add(new PaneRect(
                id: paneIds[i],
                x: i * colWidth,
                y: 0,
                width: colWidth,
                height: containerSize.Height));
        }
        return rects;
    }

    private List<PaneRect> LayoutRows(List<string> paneIds, ContainerSize containerSize)
    {
        int count = paneIds.Count;
        double rowHeight = containerSize.Height / count;

        var rects = new List<PaneRect>();
        for (int i = 0; i < count; i++)
        {
            rects.Add(new PaneRect(
                id: paneIds[i],
                x: 0,
                y: i * rowHeight,
                width: containerSize.Width,
                height: rowHeight));
        }
        return rects;
    }

    private List<PaneRect> LayoutGrid(List<string> paneIds, ContainerSize containerSize)
    {
        int count = paneIds.Count;

        // Calculate grid dimensions
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);

        double colWidth = containerSize.Width / cols;
        double rowHeight = containerSize.Height / rows;

        var rects = new List<PaneRect>();
        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            rects.Add(new PaneRect(
                id: paneIds[i],
                x: col * colWidth,
                y: row * rowHeight,
                width: colWidth,
                height: rowHeight));
        }
        return rects;
    }

    private List<PaneRect> LayoutMainLeft(List<string> paneIds, ContainerSize containerSize)
    {
        if (paneIds.Count < 2)
        {
            return LayoutSingle(paneIds, containerSize);
        }

        // Main pane on left takes 50%, rest stacked on right
        double mainWidth = containerSize.Width * 0.5;
        double sideWidth = containerSize.Width * 0.5;

        var rects = new List<PaneRect>();

        // Main pane
        rects.Add(new PaneRect(
            id: paneIds[0],
            x: 0,
            y: 0,
            width: mainWidth,
            height: containerSize.Height));

        // Side panes stacked vertically
        int sideCount = paneIds.Count - 1;
        double sideHeight = containerSize.Height / sideCount;

        for (int i = 0; i < sideCount; i++)
        {
            rects.Add(new PaneRect(
                id: paneIds[i + 1],
                x: mainWidth,
                y: i * sideHeight,
                width: sideWidth,
                height: sideHeight));
        }

        return rects;
    }

    private List<PaneRect> LayoutMainRight(List<string> paneIds, ContainerSize containerSize)
    {
        if (paneIds.Count < 2)
        {
            return LayoutSingle(paneIds, containerSize);
        }

        // Main pane on right takes 50%, rest stacked on left
        double sideWidth = containerSize.Width * 0.5;
        double mainWidth = containerSize.Width * 0.5;

        var rects = new List<PaneRect>();

        // Side panes stacked vertically
        int sideCount = paneIds.Count - 1;
        double sideHeight = containerSize.Height / sideCount;

        for (int i = 0; i < sideCount; i++)
        {
            rects.Add(new PaneRect(
                id: paneIds[i + 1],
                x: 0,
                y: i * sideHeight,
                width: sideWidth,
                height: sideHeight));
        }

        // Main pane
        rects.Add(new PaneRect(
            id: paneIds[0],
            x: sideWidth,
            y: 0,
            width: mainWidth,
            height: containerSize.Height));

        return rects;
    }

    private List<PaneRect> LayoutAuto(List<string> paneIds, ContainerSize containerSize)
    {
        var actualPreset = AutoPreset(paneIds.Count);
        return Calculate(paneIds, actualPreset, containerSize);
    }
}
