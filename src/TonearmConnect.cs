using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FluentValidation.Results;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Plugins;

namespace NzbDrone.Core.Notifications.TonearmConnect
{
    /// <summary>
    /// "Tonearm Connect": lets the Tonearm apps on your phone and desktop see and control each other,
    /// through the Lidarr both of them already reach (with its API key, behind the same proxy).
    ///
    /// Everything goes through Lidarr's provider action endpoint:
    /// <c>POST /api/v1/notification/action/tonearm?op=…</c> with a TonearmConnect resource as the body
    /// (its hidden Payload field carries the data). The connection doesn't need to be added in Lidarr.
    /// </summary>
    public class TonearmConnect : NotificationBase<TonearmConnectSettings>
    {
        /// <summary>2 added the shared store (ops "get" and "put").</summary>
        public const int Protocol = 2;
        private const int MaxWaitSeconds = 25;

        public TonearmConnect(IAppFolderInfo appFolderInfo)
        {
            ConnectStore.Init(Path.Combine(appFolderInfo.AppDataFolder, "tonearm-connect"));
        }

        public override string Name => "Tonearm Connect";

        public override string Link => "https://github.com/brab-one/Tonearm-Connect";

        public override ValidationResult Test()
        {
            return new ValidationResult();
        }

        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            if (action != "tonearm")
            {
                return base.RequestAction(action, query);
            }

            var device = Get(query, "device");
            var payload = Settings?.Payload;
            if (payload != null && payload.Length > ConnectHub.MaxPayloadChars)
            {
                return new { error = "Payload too large" };
            }

            switch (Get(query, "op"))
            {
                case "hello":
                    return new { plugin = "TonearmConnect", protocol = Protocol, version = GetType().Assembly.GetName().Version?.ToString(3) };

                case "publish":
                    if (string.IsNullOrEmpty(device) || payload == null)
                    {
                        return new { error = "device and payload are required" };
                    }

                    return new { ok = true, seq = ConnectHub.Publish(device, payload) };

                case "devices":
                    return new { devices = ConnectHub.List() };

                case "send":
                    var target = Get(query, "target");
                    if (string.IsNullOrEmpty(device) || string.IsNullOrEmpty(target) || payload == null)
                    {
                        return new { error = "device, target and payload are required" };
                    }

                    return new { ok = true, seq = ConnectHub.Send(device, target, payload) };

                case "poll":
                    if (string.IsNullOrEmpty(device))
                    {
                        return new { error = "device is required" };
                    }

                    long.TryParse(Get(query, "after"), out var after);
                    int.TryParse(Get(query, "wait"), out var wait);
                    var (commands, seq) = ConnectHub.Poll(device, after, TimeSpan.FromSeconds(Math.Clamp(wait, 0, MaxWaitSeconds)));
                    return new { commands = commands.Select(c => new { seq = c.Seq, from = c.From, payload = c.Payload }).ToList(), seq };

                case "get":
                {
                    var key = Get(query, "key");
                    if (!ConnectStore.ValidKey(key))
                    {
                        return new { error = "a valid key is required" };
                    }

                    var entry = ConnectStore.Get(key);
                    return new { value = entry.Value, version = entry.Version };
                }

                case "put":
                {
                    var key = Get(query, "key");
                    if (!ConnectStore.ValidKey(key) || payload == null || !long.TryParse(Get(query, "ifVersion"), out var ifVersion))
                    {
                        return new { error = "key, ifVersion and payload are required" };
                    }

                    var (ok, current) = ConnectStore.Put(key, payload, ifVersion);
                    return ok
                        ? new { ok = true, conflict = false, value = (string)null, version = current.Version }
                        : new { ok = false, conflict = true, value = current.Value, version = current.Version };
                }

                case "forget":
                    if (!string.IsNullOrEmpty(device))
                    {
                        ConnectHub.Forget(device);
                    }

                    return new { ok = true };

                default:
                    return new { error = "Unknown op" };
            }
        }

        private static string Get(IDictionary<string, string> query, string key)
        {
            return query != null && query.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>
    /// Shows the plugin under Lidarr's System → Plugins. Owner and name must match the GitHub repository:
    /// Lidarr installs it into plugins/&lt;owner&gt;/&lt;repo&gt; and checks that repository's releases for updates.
    /// </summary>
    public sealed class TonearmConnectPlugin : Plugin
    {
        public override string Name => "Tonearm-Connect";
        public override string Owner => "brab-one";
        public override string GithubUrl => "https://github.com/brab-one/Tonearm-Connect";
    }
}
