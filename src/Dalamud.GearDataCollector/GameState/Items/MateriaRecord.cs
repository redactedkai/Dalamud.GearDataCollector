namespace GearDataCollector.GameState.Items
{
    /// <summary>
    /// One materia melded into an item.
    /// </summary>
    /// <param name="MateriaId">The Materia sheet row id (the materia type).</param>
    /// <param name="Grade">The grade index (0 is the first grade).</param>
    /// <param name="MateriaItemId">The Item row id of that materia, or 0 if unknown.</param>
    /// <param name="ParamId">The BaseParam row id the materia raises.</param>
    /// <param name="ParamName">The BaseParam name the materia raises.</param>
    /// <param name="Value">The stat value the materia gives (Materia sheet), unsynced.</param>
    internal sealed record MateriaRecord(uint MateriaId, int Grade, uint MateriaItemId, uint ParamId, string ParamName, int Value);
}
