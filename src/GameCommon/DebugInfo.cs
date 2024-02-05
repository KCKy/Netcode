using System;
using System.Collections.Generic;
using System.Linq;
using Core.Client;
using Useful;

namespace GameCommon;

public sealed class DebugInfo
{
    WeightedAverage lerperFrames_ = new()
    {
        ResetTime = 10
    };

    Average drawDelta_ = new()
    {
        ResetTimeSeconds = 10
    };

    WeightedAverage frameDiff_ = new()
    {
        ResetTime = 10
    };

    public ILerperInfo? Lerper { get; set; }
    public IClient? Client { get; set; }

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
