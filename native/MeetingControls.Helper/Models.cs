using System.Text.Json.Serialization;

namespace MeetingControls.Helper;

public sealed class MeetingSession
{
    public required string Id { get; init; }
    public string Platform { get; init; } = "teams";
    public required string Title { get; init; }
    public bool InMeeting { get; init; } = true;
    public bool Picker { get; set; }
    public bool Transition { get; init; }
    public bool? Muted { get; init; }
    public bool? CameraOn { get; init; }
    public bool? Sharing { get; init; }
    public MeetingSession? LastKnown { get; set; }
    public Dictionary<string, bool> Capabilities { get; init; } = [];
}

public sealed class HelperCommand
{
    public string RequestId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string SessionId { get; init; } = "";
}

internal sealed record StateRecord(IReadOnlyList<MeetingSession> Sessions, string Error)
{
    public string Type => "state";
}

internal sealed record ResultRecord(string RequestId, bool Ok, string? Error = null)
{
    public string Type => "result";
}
