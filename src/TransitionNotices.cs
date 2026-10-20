using System;
using System.Collections.Generic;

public sealed class MeetingTransitionNotice {
    public string Kind { get; private set; }
    public bool State { get; private set; }
    public MeetingTransitionNotice(string kind, bool state) { Kind = kind; State = state; }
}
public sealed class MeetingTransitionNotices {
    sealed class Pending { public bool Before; public long Expires; }
    readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
    public void Begin(string session, string kind, bool before, long now) {
        if (kind != "microphone" && kind != "camera") return;
        pending[session + "/" + kind] = new Pending { Before = before, Expires = now + 3000 };
    }
    public MeetingTransitionNotice Observe(string session, string kind, bool? state, long now) {
        string key = session + "/" + kind;
        Pending item;
        if (!pending.TryGetValue(key, out item)) return null;
        if (now > item.Expires) { pending.Remove(key); return null; }
        if (!state.HasValue || state.Value == item.Before) return null;
        pending.Remove(key);
        return new MeetingTransitionNotice(kind, state.Value);
    }
    public void Prune(long now) {
        var expired = new List<string>();
        foreach (var item in pending) if (now > item.Value.Expires) expired.Add(item.Key);
        foreach (var key in expired) pending.Remove(key);
    }
}
