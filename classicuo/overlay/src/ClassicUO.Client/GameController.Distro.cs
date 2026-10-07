// SPDX-License-Identifier: BSD-2-Clause

// The window title this distribution gives the client, kept here so
// GameController.cs only carries the calls into it.

namespace ClassicUO
{
    internal unsafe partial class GameController
    {
        /// <summary>
        ///     "WispUO 1.0.0", after the character's name once in game.
        /// </summary>
        private static string DistroWindowTitle(string character)
        {
#if DEV_BUILD
            string product = $"{WispUO.NameAndVersion} [dev]";
#else
            string product = WispUO.NameAndVersion;
#endif

            return string.IsNullOrEmpty(character) ? product : $"{character} - {product}";
        }
    }
}
