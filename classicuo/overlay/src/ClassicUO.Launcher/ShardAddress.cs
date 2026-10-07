// SPDX-License-Identifier: BSD-2-Clause

using System;

namespace ClassicUO.Launcher
{
    internal enum ShardAddressError
    {
        None,
        MissingHost,
        InvalidPort
    }

    /// <summary>The host and port typed for a shard, as the shard window reads them.</summary>
    internal static class ShardAddress
    {
        public static ShardAddressError Parse(string hostText, string portText, out string host, out ushort port)
        {
            host = hostText?.Trim() ?? string.Empty;
            portText = portText?.Trim() ?? string.Empty;
            port = 0;

            // "host:port" pasted into the host field is split instead of rejected. An IPv6
            // address has colons of its own: only "[address]:port" carries a port then.
            if (host.StartsWith('['))
            {
                int close = host.IndexOf(']');

                if (close > 0)
                {
                    string rest = host.Substring(close + 1);

                    if (rest.StartsWith(':') && ushort.TryParse(rest.AsSpan(1), out _))
                    {
                        portText = rest.Substring(1);
                    }

                    host = host.Substring(1, close - 1);
                }
            }
            else if (host.IndexOf(':') is var separator and > 0 && separator == host.LastIndexOf(':') &&
                     ushort.TryParse(host.AsSpan(separator + 1), out _))
            {
                portText = host.Substring(separator + 1);
                host = host.Substring(0, separator);
            }

            if (string.IsNullOrEmpty(host))
            {
                return ShardAddressError.MissingHost;
            }

            if (!ushort.TryParse(portText, out port) || port == 0)
            {
                port = 0;

                return ShardAddressError.InvalidPort;
            }

            return ShardAddressError.None;
        }
    }
}
