namespace LlmUtilityApi.Services;

public sealed class DocOptions
{
    /// <summary>Hard cap on the decoded document size accepted by the extraction tools.</summary>
    public long MaxBytes { get; set; } = 20 * 1024 * 1024;
}
