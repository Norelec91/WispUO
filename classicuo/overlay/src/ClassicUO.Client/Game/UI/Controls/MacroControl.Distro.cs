// SPDX-License-Identifier: BSD-2-Clause

// What this distribution adds to the macro editor, kept here so MacroControl.cs only
// carries the calls into it and merging upstream's file stays easy.

using System;
using System.Linq;
using ClassicUO.Game.Managers;
using ClassicUO.Resources;

namespace ClassicUO.Game.UI.Controls
{
    internal partial class MacroControl
    {
        // Not offered, though they keep their numbers so saved macros stay as they are:
        // INVALID only holds the place of the macros after it, and RazorMacro says
        // ">macro ..." for classic Razor, which Razor Enhanced does not understand.
        // The list position of a macro is no longer its number, _allHotkeys maps it.
        private static readonly MacroType[] _hidden = { MacroType.INVALID, MacroType.RazorMacro };
        private static readonly MacroType[] _allHotkeys = Enum.GetValues<MacroType>().Where(t => Array.IndexOf(_hidden, t) < 0).ToArray();
        private static readonly string[] _allHotkeysNames = _allHotkeys.Select(t => t.ToString()).ToArray();

        // the Loop checkbox (Game/Managers/MacroManager.Loop.cs)
        private Checkbox _loopBox;

        private void AddLoopBox()
        {
            _loopBox = new Checkbox
            (
                0x00D2,
                0x00D3,
                ResDistro.MacroLoop,
                0xFF,
                0xFFFF
            )
            {
                X = 0,
                Y = _hotkeyBox.Height + 57
            };

            _loopBox.ValueChanged += (sender, e) =>
            {
                if (Macro != null)
                {
                    Macro.Loop = _loopBox.IsChecked;

                    if (!Macro.Loop)
                    {
                        _gump.World.Macros.StopLoop(Macro);
                    }
                }
            };

            Add(_loopBox);
        }
    }
}
