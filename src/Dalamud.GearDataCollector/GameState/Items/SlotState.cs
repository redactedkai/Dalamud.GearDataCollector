namespace GearDataCollector.GameState.Items
{
    /// <summary>
    /// One equipped slot as the recorder compares it from frame to frame: the item, its quality and its materia
    /// (each packed as <c>(materiaId &lt;&lt; 8) | grade</c>, up to five). The default value means an empty slot.
    /// </summary>
    /// <param name="ItemId">The base item id.</param>
    /// <param name="IsHq">Whether the copy is high quality.</param>
    /// <param name="Materia0">The first materia, packed, or 0.</param>
    /// <param name="Materia1">The second materia, packed, or 0.</param>
    /// <param name="Materia2">The third materia, packed, or 0.</param>
    /// <param name="Materia3">The fourth materia, packed, or 0.</param>
    /// <param name="Materia4">The fifth materia, packed, or 0.</param>
    internal readonly record struct SlotState(uint ItemId, bool IsHq, uint Materia0, uint Materia1, uint Materia2, uint Materia3, uint Materia4);
}
