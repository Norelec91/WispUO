// SPDX-License-Identifier: BSD-2-Clause

using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Launcher.Tests
{
    /// <summary>launcher.json, the list of shards.</summary>
    public class LauncherConfigTests : WorkingDirectory
    {
        [Fact]
        public void Without_launcher_json_the_list_is_empty()
        {
            LauncherConfig config = LauncherConfig.Load();

            config.Shards.Should().BeEmpty();
            config.LastShard.Should().BeEmpty();
        }

        [Fact]
        public void A_new_shard_starts_without_assistant_and_encryption_and_remembers_the_account()
        {
            Shard shard = new Shard();

            shard.ClassicAssist.Should().BeFalse();
            shard.Encryption.Should().BeFalse();
            shard.SaveAccount.Should().BeTrue();
            shard.Port.Should().Be(2593);
            shard.ClientVersion.Should().BeEmpty();
        }

        [Fact]
        public void Shards_survive_saving_and_loading()
        {
            new LauncherConfig
            {
                Shards =
                {
                    new Shard
                    {
                        Name = "UOG Demise", Host = "login.uogdemise.com", Port = 2593, UODirectory = "/games/uo",
                        Username = "Norelec", Password = "secret", SaveAccount = false, ClassicAssist = true, Encryption = true
                    }
                },
                LastShard = "UOG Demise"
            }.Save();

            LauncherConfig loaded = LauncherConfig.Load();

            loaded.LastShard.Should().Be("UOG Demise");
            loaded.Shards.Should().ContainSingle()
                .Which.Should().BeEquivalentTo(new
                {
                    Name = "UOG Demise", Host = "login.uogdemise.com", Port = (ushort) 2593, UODirectory = "/games/uo",
                    Username = "Norelec", Password = "secret", SaveAccount = false, ClassicAssist = true, Encryption = true
                });
        }

        [Fact]
        public void Options_saved_as_null_read_as_off()
        {
            System.IO.File.WriteAllText(File("launcher.json"),
                """{ "shards": [ { "name": "Old", "host": "h", "classic_assist": null, "encryption": null } ], "last_shard": "Old" }""");

            LauncherConfig loaded = LauncherConfig.Load();

            Shard shard = loaded.Shards.Should().ContainSingle().Subject;
            shard.Name.Should().Be("Old");
            shard.ClassicAssist.Should().BeFalse();
            shard.Encryption.Should().BeFalse();
        }

        [Fact]
        public void An_unreadable_file_is_kept_aside_rather_than_lost()
        {
            System.IO.File.WriteAllText(File("launcher.json"), "{ not json");

            LauncherConfig.Load().Shards.Should().BeEmpty();

            System.IO.File.ReadAllText(File("launcher.json.broken")).Should().Be("{ not json");
        }

        [Fact]
        public void The_file_uses_snake_case_keys()
        {
            new LauncherConfig { Shards = { new Shard { Name = "S", UODirectory = "/uo", ClassicAssist = true } } }.Save();

            JsonObject shard = JsonNode.Parse(System.IO.File.ReadAllText(File("launcher.json")))!["shards"]![0]!.AsObject();

            shard.ContainsKey("uo_directory").Should().BeTrue();
            shard.ContainsKey("classic_assist").Should().BeTrue();
            shard.ContainsKey("save_account").Should().BeTrue();
            shard.ContainsKey("summary").Should().BeFalse("Summary is only for the list");
        }
    }
}
