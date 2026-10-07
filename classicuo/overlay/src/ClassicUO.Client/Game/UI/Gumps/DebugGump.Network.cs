// SPDX-License-Identifier: BSD-2-Clause

// The connection line this distribution adds at the bottom of the Debug panel: what the
// Connection button (NetworkStatsGump) used to show on its own. Kept here so
// DebugGump.cs only carries the two calls into it.

using System;
using ClassicUO.Network;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal partial class DebugGump
    {
        private uint _ping, _deltaBytesReceived, _deltaBytesSent;
        private string _netText = string.Empty;
        private int _netTextY;

        /// <summary>
        ///     Refreshes the connection line, the ping alone when compact, ping and
        ///     traffic when expanded, and returns the size of the whole text.
        /// </summary>
        private Vector2 AddNetLine()
        {
            // the expanded text ends with a newline, which would leave a gap above the line
            _cacheText = _cacheText.TrimEnd('\n');

            if (NetClient.Socket.IsConnected)
            {
                _ping = NetClient.Socket.Statistics.Ping;
                _deltaBytesReceived = NetClient.Socket.Statistics.DeltaBytesReceived;
                _deltaBytesSent = NetClient.Socket.Statistics.DeltaBytesSent;
            }

            _netText = IsMinimized
                ? $"- Ping: {_ping} ms   In: {NetStatistics.GetSizeAdaptive(_deltaBytesReceived)}   Out: {NetStatistics.GetSizeAdaptive(_deltaBytesSent)}"
                : $"Ping: {_ping} ms";

            Vector2 size = Fonts.Bold.MeasureString(_cacheText);
            Vector2 netSize = Fonts.Bold.MeasureString(_netText);
            _netTextY = (int) size.Y;

            return new Vector2(Math.Max(size.X, netSize.X), size.Y + netSize.Y);
        }

        private void DrawNetLine(UltimaBatcher2D batcher, int x, int y, float layerDepth)
        {
            // the connection line takes the colour of the ping
            Vector3 pingHue = ShaderHueTranslator.GetHueVector(0);
            pingHue.X = _ping < 150 ? 0x44 /* green */ : _ping < 200 ? 0x34 /* yellow */ : _ping < 300 ? 0x31 /* orange */ : 0x20 /* red */;
            pingHue.Y = 1;

            batcher.DrawString
            (
                Fonts.Bold,
                _netText,
                x + 10,
                y + 10 + _netTextY,
                pingHue,
                layerDepth
            );
        }
    }
}
