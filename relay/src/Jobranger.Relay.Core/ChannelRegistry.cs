namespace Jobranger.Relay.Core;

public sealed class ChannelRegistry(TimeProvider time) : IDisposable
{
    private static readonly Presence offline = new(false, 0);

    private readonly Dictionary<ChannelId, ChannelMembers> channels = [];
    private readonly Lock gate = new();

    public int ActiveChannels
    {
        get
        {
            lock (gate)
            {
                return channels.Count;
            }
        }
    }

    public JoinOutcome Admits(ChannelId channel, RelayRole role, string? grant)
    {
        lock (gate)
        {
            return channels.TryGetValue(channel, out ChannelMembers? members) ? members.Admits(role, grant) : JoinOutcome.Joined;
        }
    }

    public JoinResult TryJoin(ChannelId channel, RelayRole role, string connectionId, string? grant)
    {
        lock (gate)
        {
            if (!channels.TryGetValue(channel, out ChannelMembers? members))
            {
                members = new ChannelMembers(time);
                channels.Add(channel, members);
            }

            return members.Join(role, connectionId, grant);
        }
    }

    public IReadOnlyList<string> Members(ChannelId channel, RelayRole role)
    {
        lock (gate)
        {
            return channels.TryGetValue(channel, out ChannelMembers? members) ? members.Members(role) : [];
        }
    }

    public Departure? Leave(ChannelId channel, RelayRole role, string connectionId)
    {
        lock (gate)
        {
            if (!channels.TryGetValue(channel, out ChannelMembers? members))
            {
                return null;
            }

            Departure? departure = members.Leave(role, connectionId);
            if (members.IsEmpty)
            {
                channels.Remove(channel);
                members.Dispose();
            }

            return departure;
        }
    }

    public Presence Snapshot(ChannelId channel)
    {
        lock (gate)
        {
            return channels.TryGetValue(channel, out ChannelMembers? members) ? members.Presence : offline;
        }
    }

    public SidOutcome BindSid(ChannelId channel, string connectionId, string sid)
    {
        lock (gate)
        {
            return channels.TryGetValue(channel, out ChannelMembers? members) ? members.Bind(connectionId, sid) : SidOutcome.Taken;
        }
    }

    public bool TryConsumeBytes(ChannelId channel, RelayRole sender, int bytes)
    {
        lock (gate)
        {
            return channels.TryGetValue(channel, out ChannelMembers? members) && members.TryConsumeBytes(sender, bytes);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (ChannelMembers members in channels.Values)
            {
                members.Dispose();
            }

            channels.Clear();
        }
    }
}
