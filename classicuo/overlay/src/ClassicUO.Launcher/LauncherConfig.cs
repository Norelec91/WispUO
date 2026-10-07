// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Launcher
{
    public sealed class Shard
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public ushort Port { get; set; } = 2593;

        /// <summary>The Ultima Online files this shard is played with: shards differ in what they need.</summary>
        public string UODirectory { get; set; } = string.Empty;

        /// <summary>Empty: the client reads the version from client.exe.</summary>
        public string ClientVersion { get; set; } = string.Empty;

        // The last account used on this shard, as the client saved it in settings.json
        // (password scrambled the client's way). Never typed in the launcher: picked up
        // after a session, put back on Play so the login screen shows this shard's.
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool SaveAccount { get; set; } = true;

        /// <summary>Whether the client loads ClassicAssist on this shard: off unless asked for.</summary>
        [JsonConverter(typeof(NullAsFalse))]
        public bool ClassicAssist { get; set; }

        /// <summary>Whether the client encrypts its traffic, which only a few shards ask for.</summary>
        [JsonConverter(typeof(NullAsFalse))]
        public bool Encryption { get; set; }

        [JsonIgnore]
        public string Summary => $"{Host}:{Port}";
    }

    /// <summary>
    ///     The launcher's own file, launcher.json, next to settings.json: the client
    ///     knows a single shard, the launcher keeps the list.
    /// </summary>
    public sealed class LauncherConfig
    {
        private const string FILENAME = "launcher.json";

        public List<Shard> Shards { get; set; } = new List<Shard>();
        public string LastShard { get; set; } = string.Empty;

        // The client keeps its files in the working directory (CUOEnviroment.ExecutablePath),
        // so the launcher does the same and both end up side by side.
        private static string FilePath => Path.Combine(Environment.CurrentDirectory, FILENAME);

        public static LauncherConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize(File.ReadAllText(FilePath), LauncherJsonContext.Default.LauncherConfig)
                        ?? new LauncherConfig();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"cannot read {FilePath}: {ex.Message}");

                // The empty list returned instead would be saved over it on the next change:
                // the shards the file held stay in a copy.
                try
                {
                    File.Copy(FilePath, FilePath + ".broken", overwrite: true);
                }
                catch (Exception)
                {
                    // nothing more to do than starting empty
                }
            }

            return new LauncherConfig();
        }

        public void Save()
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, LauncherJsonContext.Default.LauncherConfig));
        }
    }

    /// <summary>
    ///     A null read as false: shards saved when these options could be unset have
    ///     "classic_assist": null or "encryption": null, which a bool would refuse.
    /// </summary>
    internal sealed class NullAsFalse : JsonConverter<bool>
    {
        public override bool HandleNull => true;

        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType != JsonTokenType.Null && reader.GetBoolean();
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    [JsonSerializable(typeof(LauncherConfig))]
    internal partial class LauncherJsonContext : JsonSerializerContext
    {
    }
}
