using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YOLOTerm.Core.Layout;

namespace YOLOTerm.App.Controls;

/// <summary>
/// WPF Panel that consumes LayoutEngine output and hosts multiple PaneControls
/// </summary>
public class TilingPanel : Panel
{
    private readonly LayoutEngine _engine = new();
    private string? _zoomedPane;

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.Register(
            nameof(Preset),
            typeof(LayoutPreset),
            typeof(TilingPanel),
            new FrameworkPropertyMetadata(
                LayoutPreset.Auto,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public LayoutPreset Preset
    {
        get => (LayoutPreset)GetValue(PresetProperty);
        set => SetValue(PresetProperty, value);
    }

    public string? ZoomedPane
    {
        get => _zoomedPane;
        set
        {
            if (_zoomedPane != value)
            {
                _zoomedPane = value;
                InvalidateArrange();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Let each pane measure itself with infinite size
        // The LayoutEngine will determine actual sizes
        var infiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
        
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(infiniteSize);
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0)
        {
            return finalSize;
        }

        // Get pane IDs from children
        var paneIds = new List<string>();
        foreach (UIElement child in InternalChildren)
        {
            if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                paneIds.Add(fe.Name);
            }
            else
            {
                // Generate ID if not named
                paneIds.Add($"pane-{InternalChildren.IndexOf(child)}");
            }
        }

        // Calculate layout using engine
        var containerSize = new ContainerSize(finalSize.Width, finalSize.Height);
        var layout = _engine.Calculate(
            paneIds,
            Preset,
            containerSize,
            zoomedPane: _zoomedPane);

        // Arrange children according to layout
        for (int i = 0; i < InternalChildren.Count && i < layout.Count; i++)
        {
            var child = InternalChildren[i];
            var rect = layout[i];

            child.Arrange(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        return finalSize;
    }

    public void Equalize()
    {
        InvalidateArrange();
    }

    public void ZoomPane(string? paneId)
    {
        ZoomedPane = paneId;
    }
}
