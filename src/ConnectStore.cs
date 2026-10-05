using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Notifications.TonearmConnect
{
    /// <summary>
    /// Small JSON documents the Tonearm apps share (e.g. likes of songs that aren't downloaded yet),
    /// saved in Lidarr's app data folder so they survive restarts. Each value has a version; a write
    /// only goes through when it names the current version, so devices never overwrite each other.
    /// </summary>
    public static class ConnectStore
    {
        private static readonly object Gate = new object();
        private static readonly Regex KeyPattern = new Regex("^[a-z0-9][a-z0-9-]{0,63}$");
        private static Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private static string _file;

        public sealed class Entry
        {
            public long Version { get; set; }
            public string Value { get; set; }
        }

        public static bool ValidKey(string key) => key != null && KeyPattern.IsMatch(key);

        /// <summary>Points the store at its file (once); later calls are ignored.</summary>
        public static void Init(string folder)
        {
            lock (Gate)
            {
                if (_file != null)
                {
                    return;
                }

                Directory.CreateDirectory(folder);
                _file = Path.Combine(folder, "store.json");
                if (File.Exists(_file))
                {
                    try
                    {
                        _entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(_file)) ?? new Dictionary<string, Entry>();
                    }
                    catch (JsonException)
                    {
                        File.Copy(_file, _file + ".broken", true);
                        _entries = new Dictionary<string, Entry>();
                    }
                }
            }
        }

        public static Entry Get(string key)
        {
            lock (Gate)
            {
                return _entries.TryGetValue(key, out var entry) ? entry : new Entry { Version = 0, Value = null };
            }
        }

        /// <summary>Saves [value] if the stored version is still [ifVersion]; returns whether it did and what's stored now.</summary>
        public static (bool Ok, Entry Current) Put(string key, string value, long ifVersion)
        {
            lock (Gate)
            {
                var current = _entries.TryGetValue(key, out var entry) ? entry : new Entry { Version = 0, Value = null };
                if (current.Version != ifVersion)
                {
                    return (false, current);
                }

                var next = new Entry { Version = current.Version + 1, Value = value };
                _entries[key] = next;
                Save();
                return (true, next);
            }
        }

        private static void Save()
        {
            if (_file == null)
            {
                return;
            }

            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries));
            File.Move(tmp, _file, true);
        }
    }
}
