namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>How a <see cref="CassetteHandler"/> behaves for a given HTTP call.</summary>
public enum CassetteMode
{
    /// <summary>Pass every request straight through. No recording, no replay.</summary>
    Off,

    /// <summary>
    /// Always call the real API, and (over)write the cassette with what came back. Use this to
    /// deliberately refresh a cassette, e.g. after the API's response shape changes.
    /// </summary>
    Record,

    /// <summary>
    /// Never call the real API. Every request must match a recorded interaction, or a
    /// <see cref="CassetteException"/> is thrown. Fully offline and needs no API key — the mode
    /// CI should run in.
    /// </summary>
    Replay,

    /// <summary>
    /// Replay a request if the cassette already has a matching interaction; otherwise call the
    /// real API and append the result to the cassette. The common default: run once locally with
    /// a real key to record, then every later run (including CI) replays for free.
    /// </summary>
    RecordOnce,
}
