// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClassicUO.Launcher
{
    /// <summary>
    ///     The client's settings.json, edited as a JSON tree so that only the keys the
    ///     launcher owns change and everything else the client wrote survives as is.
    /// </summary>
    internal sealed class ClientSettings
    {
        private readonly JsonObject _root;

        private ClientSettings(JsonObject root)
        {
            _root = root;
        }

        private static string FilePath => Path.Combine(Environment.CurrentDirectory, "settings.json");

        /// <summary>ClassicAssist as settings.json lists it: plugin paths are relative to Data/Plugins.</summary>
        public const string ClassicAssistPlugin = "ClassicAssist/ClassicAssist.dll";

        public static bool ClassicAssistInstalled =>
            File.Exists(Path.Combine(Environment.CurrentDirectory, "Data", "Plugins", ClassicAssistPlugin));

        public static ClientSettings Load()
        {
            try
            {
                if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject root)
                {
                    return new ClientSettings(root);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"cannot read {FilePath}: {ex.Message}");
            }

            return new ClientSettings(new JsonObject());
        }

        public string Ip => GetString("ip");
        // the client also accepts the port written as a string
        public ushort Port => ushort.TryParse(_root["port"]?.ToString(), out ushort port) && port != 0 ? port : (ushort)2593;
        public string UltimaOnlineDirectory => GetString("ultimaonlinedirectory");
        public string ClientVersion => GetString("clientversion");
        public string Username => GetString("username");
        public string Password => GetString("password");
        public bool SaveAccount => _root["saveaccount"] is JsonValue v && v.TryGetValue(out bool save) && save;

        /// <summary>
        ///     Whether settings.json points at <paramref name="shard"/>, i.e. the account the
        ///     client saved there was used on it.
        /// </summary>
        public bool PointsAt(Shard shard)
        {
            return string.Equals(Ip, shard.Host, StringComparison.OrdinalIgnoreCase) && Port == shard.Port;
        }

        /// <summary>
        ///     Points the client at <paramref name="shard"/>, the game files it is played with
        ///     and the account last used on it. A shard with no account saved gets empty fields
        ///     rather than another shard's.
        /// </summary>
        public void Apply(Shard shard)
        {
            _root["ip"] = shard.Host;
            _root["port"] = shard.Port;
            _root["ultimaonlinedirectory"] = shard.UODirectory;
            _root["clientversion"] = shard.ClientVersion;
            _root["username"] = shard.Username;
            _root["password"] = shard.Password;
            _root["saveaccount"] = shard.SaveAccount;

            // the client reads 0 (none) or 1 (the encryption of its client version)
            _root["encryption"] = shard.Encryption ? 1 : 0;

            SetClassicAssist(shard.ClassicAssist);
        }

        /// <summary>
        ///     Adds ClassicAssist to the plugin list or takes it out. Any other plugin
        ///     listed there is the user's business and stays.
        /// </summary>
        private void SetClassicAssist(bool enabled)
        {
            JsonArray plugins = new JsonArray();

            if (_root["plugins"] is JsonArray current)
            {
                foreach (JsonNode node in current)
                {
                    if (node is JsonValue v && v.TryGetValue(out string path) && !IsClassicAssist(path))
                    {
                        plugins.Add((JsonNode)JsonValue.Create(path));
                    }
                }
            }

            if (enabled)
            {
                plugins.Add((JsonNode)JsonValue.Create(ClassicAssistPlugin));
            }

            _root["plugins"] = plugins;
        }

        /// <summary>The same test the client runs at startup.</summary>
        public static bool IsValidUODirectory(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && Directory.Exists(path)
                && File.Exists(Path.Combine(path, "tiledata.mul"));
        }

        private static bool IsClassicAssist(string path)
        {
            return string.Equals(Path.GetFileName(path.Replace('\\', '/')), "ClassicAssist.dll", StringComparison.OrdinalIgnoreCase);
        }

        public void Save()
        {
            // Written out in full before the file is touched: a failure halfway must not
            // leave the user with an empty settings.json.
            using MemoryStream buffer = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                _root.WriteTo(writer);
            }

            File.WriteAllBytes(FilePath, buffer.ToArray());
        }

        private string GetString(string key)
        {
            return _root[key] is JsonValue v && v.TryGetValue(out string s) ? s : string.Empty;
        }
    }
}
