using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NzbDrone.Core.Notifications.TonearmConnect
{
    /// <summary>
    /// The relay between Tonearm devices, kept in memory: each device publishes its playback state,
    /// sends commands to other devices, and long-polls for the commands sent to it. Nothing is
    /// stored; after a Lidarr restart the devices simply announce themselves again.
    /// </summary>
    public static class ConnectHub
    {
        /// <summary>A device that hasn't polled or published for this long is shown as offline.</summary>
        public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(45);
        public const int MaxPayloadChars = 2_000_000;
        private const int MaxQueuedCommands = 50;
        private static readonly TimeSpan ForgetAfter = TimeSpan.FromDays(7);

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Device> Devices = new Dictionary<string, Device>();
        private static readonly Dictionary<string, List<Command>> Inboxes = new Dictionary<string, List<Command>>();
        private static long _seq;

        private sealed class Device
        {
            public string State;
            public DateTime LastSeen;
        }

        public sealed class Command
        {
            public long Seq { get; set; }
            public string From { get; set; }
            public string Payload { get; set; }
        }

        public sealed class DeviceEntry
        {
            public string Id { get; set; }
            public bool Online { get; set; }
            public long SecondsSinceSeen { get; set; }
            /// <summary>The device's own JSON, passed through untouched.</summary>
            public string State { get; set; }
        }

        public static long Publish(string device, string state)
        {
            lock (Gate)
            {
                Touch(device).State = state;
                Monitor.PulseAll(Gate);
                return _seq;
            }
        }

        public static List<DeviceEntry> List()
        {
            lock (Gate)
            {
                var now = DateTime.UtcNow;
                foreach (var stale in Devices.Where(d => now - d.Value.LastSeen > ForgetAfter).Select(d => d.Key).ToList())
                {
                    Devices.Remove(stale);
                    Inboxes.Remove(stale);
                }

                return Devices.Select(d => new DeviceEntry
                {
                    Id = d.Key,
                    Online = now - d.Value.LastSeen < OnlineWindow,
                    SecondsSinceSeen = (long)(now - d.Value.LastSeen).TotalSeconds,
                    State = d.Value.State,
                }).OrderBy(d => d.Id).ToList();
            }
        }

        public static long Send(string from, string target, string payload)
        {
            lock (Gate)
            {
                if (!Inboxes.TryGetValue(target, out var inbox))
                {
                    inbox = new List<Command>();
                    Inboxes[target] = inbox;
                }

                var seq = ++_seq;
                inbox.Add(new Command { Seq = seq, From = from, Payload = payload });
                if (inbox.Count > MaxQueuedCommands)
                {
                    inbox.RemoveRange(0, inbox.Count - MaxQueuedCommands);
                }

                Monitor.PulseAll(Gate);
                return seq;
            }
        }

        /// <summary>
        /// Commands for [device] newer than [after], waiting up to [wait] for one to arrive. Polling
        /// also counts as being online. Returns the newest sequence number the device can resume from.
        /// </summary>
        public static (List<Command> Commands, long Seq) Poll(string device, long after, TimeSpan wait)
        {
            var deadline = DateTime.UtcNow + wait;
            lock (Gate)
            {
                Touch(device);
                while (true)
                {
                    var pending = Inboxes.TryGetValue(device, out var inbox)
                        ? inbox.Where(c => c.Seq > after).ToList()
                        : new List<Command>();
                    var left = deadline - DateTime.UtcNow;
                    if (pending.Count > 0 || left <= TimeSpan.Zero)
                    {
                        Touch(device);
                        return (pending, Math.Max(after, pending.Count > 0 ? pending.Max(c => c.Seq) : after));
                    }

                    Monitor.Wait(Gate, left);
                }
            }
        }

        public static void Forget(string device)
        {
            lock (Gate)
            {
                Devices.Remove(device);
                Inboxes.Remove(device);
                Monitor.PulseAll(Gate);
            }
        }

        private static Device Touch(string device)
        {
            if (!Devices.TryGetValue(device, out var entry))
            {
                entry = new Device();
                Devices[device] = entry;
            }

            entry.LastSeen = DateTime.UtcNow;
            return entry;
        }
    }
}
