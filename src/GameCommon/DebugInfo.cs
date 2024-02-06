using Core.Client;
using Useful;

namespace GameCommon;

/// <summary>
/// General debug info collection for clients.
/// </summary>
public sealed class DebugInfo
{
    TimeWeightedAverage lerperFrames_ = new()
    {
        ResetTime = 10
    };

    Average drawDelta_ = new()
    {
        ResetTime = 10
    };

    TimeWeightedAverage frameDiff_ = new()
    {
        ResetTime = 10
    };

    /// <summary>
    /// Optional reference a to a lerper.
    /// If provided, statistics about the lerper's function are provided.
    /// </summary>
    public ILerperInfo? Lerper { get; set; }
    
    /// <summary>
    /// Optional reference to the main client object.
    /// If provided, statistics about the client's function are provided.
    /// </summary>
    public IClient? Client { get; set; }

    /// <summary>
    /// Run the debug info, shall be called every draw call (if debug mode is active)
    /// </summary>
    /// <param name="delta">The delta of this draw call.</param>
    /// <returns>String value to be debug displayed of measured statistics.</returns>
    public string Update(float delta)
    {
        long authFrame = Client?.AuthFrame ?? 0;
        long predictFrame = Client?.PredictFrame ?? 0;

        double frameDiff = frameDiff_.Update(delta, predictFrame - authFrame);
        double lerperFrames = lerperFrames_.Update(delta, Lerper?.FramesBehind ?? 0);
        double fps = 1 / drawDelta_.Update(delta, delta);

        return $"Draw FPS: {fps:0.00}\n" +
               $"Auth Frame: {authFrame}\n" +
               $"Predict Frame: {predictFrame}\n" +
               $"Avg frame diff: {frameDiff:0.00}\n" +
               $"Lerper frames behind: {lerperFrames:0.00}\n" +
               $"Client ID: {Client?.Id}\n" +
               $"Current TPS: {Client?.CurrentTps:0.00}\n" +
               $"Target TPS: {Client?.TargetTps:0.00}\n" +
               $"Current Delta: {Client?.CurrentDelta:0.00}\n" +
               $"Target Delta: {Client?.TargetDelta:0.00}\n" +
               $"Predict Delay Margin: {Client?.PredictDelayMargin:0.00}\n" +
               $"TraceState: {Client?.TraceState}\n" +
               $"Checksum: {Client?.UseChecksum}\n" +
               $"TraceTime: {Client?.TraceFrameTime}\n";
    }
}
