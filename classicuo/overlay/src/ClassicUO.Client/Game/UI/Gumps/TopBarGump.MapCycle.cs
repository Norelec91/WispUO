// SPDX-License-Identifier: BSD-2-Clause

// What this distribution adds to the top bar, kept here so TopBarGump.cs only carries the
// calls into it and merging upstream's file stays easy.

using ClassicUO.Game.Managers;

namespace ClassicUO.Game.UI.Gumps
{
    internal partial class TopBarGump
    {
        /// <summary>
        ///     The Map button goes through three maps and starts over: the small radar, the
        ///     large radar, then the world map, which takes the radar's place.
        /// </summary>
        private void CycleMap()
        {
            MiniMapGump miniMap = UIManager.GetGump<MiniMapGump>();
            WorldMapGump worldMap = UIManager.GetGump<WorldMapGump>();

            if (miniMap != null)
            {
                // small -> large; large -> the world map
                if (miniMap.ToggleSize())
                {
                    miniMap.SetInScreen();
                    miniMap.BringOnTop();
                }
                else
                {
                    miniMap.Dispose();
                    GameActions.OpenWorldMap(World);
                }

                return;
            }

            // nothing open, or the world map: back to the small radar
            worldMap?.Dispose();
            UIManager.Add(new MiniMapGump(World));
        }
    }
}
