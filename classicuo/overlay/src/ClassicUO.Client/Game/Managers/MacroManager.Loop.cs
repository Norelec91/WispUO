// SPDX-License-Identifier: BSD-2-Clause

// Looping macros, a feature of this distribution. It lives here so MacroManager.cs only
// carries the calls into it, and merging upstream's file stays easy.
//
// A macro flagged as looping restarts from its first action when it ends, until
// the same hotkey is pressed again or ESC stops it. Rewinding happens once per
// frame with a 250ms floor, so a macro without a Delay cannot lock the client up
// or flood the server. The macro button turns green while the loop runs.

using System.Xml;

namespace ClassicUO.Game.Managers
{
    internal sealed partial class MacroManager
    {
        // minimum amount of time between two iterations of a looping macro.
        // A macro without any Delay would otherwise be executed once per frame.
        private const long LOOP_MIN_DELAY = 250;

        private Macro _loopingMacro;

        /// <summary>
        ///     Starts a macro. A macro flagged as <see cref="Macro.Loop"/> behaves like a toggle:
        ///     running it while it is already looping stops it.
        /// </summary>
        public void SetMacroToExecute(Macro macro)
        {
            if (macro == null)
            {
                return;
            }

            if (_loopingMacro == macro)
            {
                StopLoop();

                return;
            }

            _loopingMacro = macro.Loop ? macro : null;
            _lastMacro = macro.Items as MacroObject;
        }

        public bool IsLooping(Macro macro)
        {
            return macro != null && _loopingMacro == macro;
        }

        public void StopLoop()
        {
            if (_loopingMacro != null)
            {
                _loopingMacro = null;
                _lastMacro = null;
                _nextTimer = 0;
            }
        }

        public void StopLoop(Macro macro)
        {
            if (_loopingMacro == macro)
            {
                StopLoop();
            }
        }

        /// <summary>
        ///     Called by Update() after each action. When a looping macro reached its end it is
        ///     rewound and true is returned: the next Update() runs the following iteration, so
        ///     a loop can never lock the client in this frame.
        /// </summary>
        private bool RewindLoop()
        {
            if (_lastMacro != null || _loopingMacro == null)
            {
                return false;
            }

            _lastMacro = _loopingMacro.Items as MacroObject;

            if (_lastMacro == null)
            {
                _loopingMacro = null;
            }
            else if (_nextTimer < Time.Ticks + LOOP_MIN_DELAY)
            {
                _nextTimer = Time.Ticks + LOOP_MIN_DELAY;
            }

            return true;
        }
    }

    internal partial class Macro
    {
        /// <summary>
        ///     When true the macro restarts from its first action as soon as it ends,
        ///     until it gets triggered again or stopped.
        /// </summary>
        public bool Loop { get; set; }

        private void WriteLoop(XmlTextWriter writer)
        {
            writer.WriteAttributeString("loop", Loop.ToString());
        }

        private void ReadLoop(XmlElement xml)
        {
            if (xml.HasAttribute("loop"))
            {
                Loop = bool.Parse(xml.GetAttribute("loop"));
            }
        }
    }
}
