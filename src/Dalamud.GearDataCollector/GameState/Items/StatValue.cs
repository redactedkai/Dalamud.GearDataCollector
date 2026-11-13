namespace GearDataCollector.GameState.Items
{
    /// <summary>
    /// One stat: a BaseParam row id, its name and a value.
    /// </summary>
    /// <param name="ParamId">The BaseParam row id.</param>
    /// <param name="Name">The BaseParam name.</param>
    /// <param name="Value">The value: from the Item sheet (unsynced) for an item, or the game's own number for a total or base stat.</param>
    internal sealed record StatValue(uint ParamId, string Name, int Value);
}
