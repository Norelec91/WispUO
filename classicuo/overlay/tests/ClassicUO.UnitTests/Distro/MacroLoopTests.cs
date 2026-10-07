// SPDX-License-Identifier: BSD-2-Clause

using System.IO;
using System.Xml;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Distro
{
    /// <summary>The Loop option of macros (MacroManager.Loop.cs).</summary>
    public class MacroLoopTests
    {
        [Fact]
        public void A_looping_macro_runs_as_a_toggle()
        {
            MacroManager manager = new MacroManager(null);
            Macro macro = new Macro("loop") { Loop = true };

            manager.SetMacroToExecute(macro);
            manager.IsLooping(macro).Should().BeTrue();

            // triggered again while it loops: it stops
            manager.SetMacroToExecute(macro);
            manager.IsLooping(macro).Should().BeFalse();
        }

        [Fact]
        public void A_macro_without_loop_does_not_loop()
        {
            MacroManager manager = new MacroManager(null);
            Macro macro = new Macro("once");

            manager.SetMacroToExecute(macro);

            manager.IsLooping(macro).Should().BeFalse();
        }

        [Fact]
        public void Starting_another_macro_ends_the_loop()
        {
            MacroManager manager = new MacroManager(null);
            Macro looping = new Macro("loop") { Loop = true };
            Macro other = new Macro("other");

            manager.SetMacroToExecute(looping);
            manager.SetMacroToExecute(other);

            manager.IsLooping(looping).Should().BeFalse();
        }

        [Fact]
        public void StopLoop_stops_only_the_macro_it_names()
        {
            MacroManager manager = new MacroManager(null);
            Macro looping = new Macro("loop") { Loop = true };

            manager.SetMacroToExecute(looping);
            manager.StopLoop(new Macro("other"));
            manager.IsLooping(looping).Should().BeTrue();

            manager.StopLoop(looping);
            manager.IsLooping(looping).Should().BeFalse();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Loop_survives_saving_and_loading(bool loop)
        {
            Macro saved = new Macro("m") { Loop = loop };

            Macro loaded = new Macro("m");
            loaded.Load(SaveToXml(saved));

            loaded.Loop.Should().Be(loop);
        }

        [Fact]
        public void A_macro_saved_before_the_option_loads_without_loop()
        {
            XmlElement xml = SaveToXml(new Macro("m") { Loop = true });
            xml.RemoveAttribute("loop");

            Macro loaded = new Macro("m");
            loaded.Load(xml);

            loaded.Loop.Should().BeFalse();
        }

        private static XmlElement SaveToXml(Macro macro)
        {
            using StringWriter text = new StringWriter();

            using (XmlTextWriter writer = new XmlTextWriter(text))
            {
                macro.Save(writer);
            }

            XmlDocument document = new XmlDocument();
            document.LoadXml(text.ToString());

            return document.DocumentElement;
        }
    }
}
