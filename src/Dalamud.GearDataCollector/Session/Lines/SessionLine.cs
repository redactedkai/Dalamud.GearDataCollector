using System;
using System.Text.Json.Serialization;

namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// One line of a session file (JSON Lines). Each kind of line is its own type; the <c>type</c> field in the JSON
    /// (written first) says which. The names and strings are the file format contract: see <see cref="SessionFormat"/>
    /// and the README.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(SessionStartLine), SessionFormat.SessionStart)]
    [JsonDerivedType(typeof(GearChangeLine), SessionFormat.GearChange)]
    [JsonDerivedType(typeof(StatsChangeLine), SessionFormat.StatsChange)]
    [JsonDerivedType(typeof(SessionEndLine), SessionFormat.SessionEnd)]
    internal abstract class SessionLine
    {
        /// <summary>Gets the UTC time the line was written.</summary>
        [JsonPropertyOrder(-1)]
        public required DateTime TimestampUtc { get; init; }
    }
}
