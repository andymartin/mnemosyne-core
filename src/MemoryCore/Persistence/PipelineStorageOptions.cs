namespace Mnemosyne.Core.Persistence;

public class PipelineStorageOptions
{
    public static string SectionName = "PipelineStorage";
    public string StoragePath { get; set; } = "pipelines";
}