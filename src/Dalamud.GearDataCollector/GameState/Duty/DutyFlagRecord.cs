namespace GearDataCollector.GameState.Duty
{
    /// <summary>
    /// What the plugin remembers about the duty it saw you enter: which duty, whether Minimum Item Level was on for it,
    /// and how that value was obtained. It is saved to a small file so it survives a game restart.
    /// </summary>
    /// <param name="TerritoryId">The territory type row id of the duty.</param>
    /// <param name="ContentFinderConditionId">The ContentFinderCondition row id of the duty.</param>
    /// <param name="MinimumItemLevelOn">Whether the queue the duty popped from had Minimum Item Level on.</param>
    /// <param name="Source">How the value was obtained (see <see cref="DutyFlagMemory"/>).</param>
    internal sealed record DutyFlagRecord(uint TerritoryId, uint ContentFinderConditionId, bool MinimumItemLevelOn, string Source);
}
