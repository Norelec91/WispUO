// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Linq;
using System.Reflection;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Distro
{
    /// <summary>What the distribution changes in the client, outside the macros.</summary>
    public class DistroTests
    {
        [Fact]
        public void The_name_and_version_read_WispUO_and_a_version()
        {
            WispUO.Name.Should().Be("WispUO");
            Version.TryParse(WispUO.Version, out _).Should().BeTrue();
            WispUO.NameAndVersion.Should().Be($"WispUO {WispUO.Version}");
        }

        [Fact]
        public void The_window_title_is_the_distribution_until_a_character_plays()
        {
            string Title(string character) =>
                (string) typeof(GameController)
                    .GetMethod("DistroWindowTitle", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, new object[] { character });

            Title(null).Should().StartWith(WispUO.NameAndVersion);
            Title(string.Empty).Should().StartWith(WispUO.NameAndVersion);
            Title("Norelec").Should().StartWith($"Norelec - {WispUO.NameAndVersion}");
        }

        [Fact]
        public void Save_Account_is_on_by_default()
        {
            new Settings().SaveAccount.Should().BeTrue();
        }

        [Fact]
        public void No_plugin_is_listed_by_default()
        {
            new Settings().Plugins.Should().BeEmpty();
        }

        [Fact]
        public void The_macro_editor_does_not_offer_INVALID_or_RazorMacro()
        {
            string[] offered = (string[]) typeof(MacroControl)
                .GetField("_allHotkeysNames", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null);

            offered.Should().NotContain(new[] { "INVALID", "RazorMacro" });
            offered.Should().Contain("Say");
        }
    }
}
