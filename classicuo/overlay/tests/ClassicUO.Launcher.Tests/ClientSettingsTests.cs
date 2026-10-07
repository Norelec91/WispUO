// SPDX-License-Identifier: BSD-2-Clause

using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace ClassicUO.Launcher.Tests
{
    /// <summary>What Play writes into the client's settings.json.</summary>
    public class ClientSettingsTests : WorkingDirectory
    {
        private static Shard Demise(bool classicAssist = false, bool encryption = false) => new Shard
        {
            Name = "UOG Demise",
            Host = "login.uogdemise.com",
            Port = 2593,
            UODirectory = "/games/uo",
            ClientVersion = "7.0.114.40",
            Username = "Norelec",
            Password = "1-352D2E363C372B36",
            SaveAccount = true,
            ClassicAssist = classicAssist,
            Encryption = encryption
        };

        private JsonObject Written() => (JsonObject) JsonNode.Parse(System.IO.File.ReadAllText(File("settings.json")));

        private static string[] Plugins(JsonObject settings) =>
            settings["plugins"]!.AsArray().Select(p => p!.GetValue<string>()).ToArray();

        private static void Play(Shard shard)
        {
            ClientSettings settings = ClientSettings.Load();
            settings.Apply(shard);
            settings.Save();
        }

        [Fact]
        public void Play_points_the_client_at_the_shard_and_its_account()
        {
            Play(Demise());

            JsonObject written = Written();
            written["ip"]!.GetValue<string>().Should().Be("login.uogdemise.com");
            written["port"]!.GetValue<int>().Should().Be(2593);
            written["ultimaonlinedirectory"]!.GetValue<string>().Should().Be("/games/uo");
            written["clientversion"]!.GetValue<string>().Should().Be("7.0.114.40");
            written["username"]!.GetValue<string>().Should().Be("Norelec");
            written["password"]!.GetValue<string>().Should().Be("1-352D2E363C372B36");
            written["saveaccount"]!.GetValue<bool>().Should().BeTrue();
        }

        [Fact]
        public void A_shard_with_ClassicAssist_lists_it_among_the_plugins()
        {
            Play(Demise(classicAssist: true));

            Plugins(Written()).Should().Equal(ClientSettings.ClassicAssistPlugin);
        }

        [Fact]
        public void A_shard_without_ClassicAssist_takes_it_out_and_keeps_the_other_plugins()
        {
            System.IO.File.WriteAllText(File("settings.json"),
                """{ "plugins": [ "Other/Other.dll", "ClassicAssist/ClassicAssist.dll" ] }""");

            Play(Demise(classicAssist: false));

            Plugins(Written()).Should().Equal("Other/Other.dll");
        }

        [Fact]
        public void ClassicAssist_is_never_listed_twice()
        {
            Play(Demise(classicAssist: true));
            Play(Demise(classicAssist: true));

            Plugins(Written()).Should().Equal(ClientSettings.ClassicAssistPlugin);
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        public void Encryption_is_written_as_the_client_reads_it(bool encryption, int expected)
        {
            Play(Demise(encryption: encryption));

            Written()["encryption"]!.GetValue<int>().Should().Be(expected);
        }

        [Fact]
        public void A_shard_does_not_inherit_the_assistant_or_encryption_of_the_previous_one()
        {
            Play(Demise(classicAssist: true, encryption: true));
            Play(new Shard { Name = "Other", Host = "login.example.com", Port = 2593, UODirectory = "/games/uo" });

            Plugins(Written()).Should().BeEmpty();
            Written()["encryption"]!.GetValue<int>().Should().Be(0);
        }

        [Fact]
        public void Keys_the_launcher_does_not_own_survive()
        {
            System.IO.File.WriteAllText(File("settings.json"), """{ "fps": 144, "lang": "ITA" }""");

            Play(Demise());

            JsonObject written = Written();
            written["fps"]!.GetValue<int>().Should().Be(144);
            written["lang"]!.GetValue<string>().Should().Be("ITA");
        }

        [Fact]
        public void PointsAt_matches_host_whatever_its_case_and_the_port()
        {
            Play(Demise());
            ClientSettings settings = ClientSettings.Load();

            settings.PointsAt(new Shard { Host = "LOGIN.UOGDEMISE.COM", Port = 2593 }).Should().BeTrue();
            settings.PointsAt(new Shard { Host = "login.uogdemise.com", Port = 2594 }).Should().BeFalse();
            settings.PointsAt(new Shard { Host = "login.uoalive.com", Port = 2593 }).Should().BeFalse();
        }

        [Fact]
        public void A_missing_settings_json_reads_as_defaults()
        {
            ClientSettings settings = ClientSettings.Load();

            settings.Ip.Should().BeEmpty();
            settings.Port.Should().Be(2593);
            settings.SaveAccount.Should().BeFalse();
        }

        [Fact]
        public void A_UO_folder_is_one_with_tiledata_mul()
        {
            string uo = Directory.CreateDirectory(File("uo")).FullName;

            ClientSettings.IsValidUODirectory(uo).Should().BeFalse();

            System.IO.File.WriteAllText(System.IO.Path.Combine(uo, "tiledata.mul"), string.Empty);
            ClientSettings.IsValidUODirectory(uo).Should().BeTrue();

            ClientSettings.IsValidUODirectory(string.Empty).Should().BeFalse();
            ClientSettings.IsValidUODirectory(File("missing")).Should().BeFalse();
        }

        [Fact]
        public void ClassicAssist_counts_as_installed_when_its_plugin_is_in_Data_Plugins()
        {
            ClientSettings.ClassicAssistInstalled.Should().BeFalse();

            string plugin = File(System.IO.Path.Combine("Data", "Plugins", ClientSettings.ClassicAssistPlugin));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(plugin)!);
            System.IO.File.WriteAllText(plugin, string.Empty);

            ClientSettings.ClassicAssistInstalled.Should().BeTrue();
        }
    }
}
