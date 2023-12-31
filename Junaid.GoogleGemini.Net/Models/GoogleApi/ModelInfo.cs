/// <summary>
/// 
/// </summary>
public class ModelInfo
{
    public string name { get; set; }
    public string version { get; set; }
    public string displayName { get; set; }
    public string description { get; set; }
    public int inputTokenLimit { get; set; }
    public int outputTokenLimit { get; set; }
    public string[] supportedGenerationMethods { get; set; }
    public float temperature { get; set; }
    public int topP { get; set; }
    public int topK { get; set; }
}