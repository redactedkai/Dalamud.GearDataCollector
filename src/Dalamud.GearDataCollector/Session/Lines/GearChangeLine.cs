using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GearDataCollector.Session.Lines
{
    /// <summary>
    /// The equipped gear changed. A swap is one entry in each list.
    /// </summary>
    internal sealed class GearChangeLine : ChangeLine
    {
        /// <summary>Gets the UTC time the equipped gear last changed before this line was written.</summary>
        [JsonPropertyOrder(8)]
        public required DateTime GearChangedUtc { get; init; }

        /// <summary>Gets the pieces that left a slot.</summary>
        public required IReadOnlyList<SessionItem> Removed { get; init; }

        /// <summary>Gets the pieces that entered a slot.</summary>
        public required IReadOnlyList<SessionItem> Added { get; init; }
    }
}
