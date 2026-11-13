namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// The last line, written when the session ends cleanly.
    /// </summary>
    internal sealed class SessionEndLine : SessionLine
    {
        /// <summary>Gets the number of change lines written.</summary>
        public required int Changes { get; init; }

        /// <summary>Gets why the session ended (one of the Reason constants in <see cref="SessionFormat"/>).</summary>
        public required string Reason { get; init; }
    }
}
