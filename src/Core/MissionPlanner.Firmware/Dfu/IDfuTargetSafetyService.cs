namespace MissionPlanner.Firmware.Dfu;

/// <summary>Evaluates target evidence without treating an STM32 identity as proof of board platform.</summary>
public interface IDfuTargetSafetyService
{
    /// <summary>Returns a typed allow, warning, or block decision before provider execution.</summary>
    DfuTargetSafetyResult Evaluate(DfuTargetSafetyRequest request);
}
