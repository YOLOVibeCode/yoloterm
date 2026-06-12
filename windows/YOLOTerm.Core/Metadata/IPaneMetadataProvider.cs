namespace YOLOTerm.Core.Metadata;

/// <summary>
/// Interface for extracting pane metadata (contracts v1)
/// </summary>
public interface IPaneMetadataProvider
{
    Task<PaneMetadata> GetMetadataAsync();
    event EventHandler<PaneMetadata>? MetadataChanged;
}
