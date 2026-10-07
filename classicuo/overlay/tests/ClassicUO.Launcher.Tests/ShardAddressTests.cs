// SPDX-License-Identifier: BSD-2-Clause

using FluentAssertions;
using Xunit;

namespace ClassicUO.Launcher.Tests
{
    /// <summary>The host and port typed in the shard window.</summary>
    public class ShardAddressTests
    {
        [Theory]
        [InlineData("login.uogdemise.com", "2593", "login.uogdemise.com", 2593)]
        [InlineData("  login.uogdemise.com  ", " 2593 ", "login.uogdemise.com", 2593)]
        [InlineData("login.uogdemise.com:2594", "2593", "login.uogdemise.com", 2594)]
        [InlineData("127.0.0.1:2593", "", "127.0.0.1", 2593)]
        [InlineData("::1", "2593", "::1", 2593)]
        [InlineData("2001:db8::1", "2593", "2001:db8::1", 2593)]
        [InlineData("[2001:db8::1]:2594", "2593", "2001:db8::1", 2594)]
        [InlineData("[::1]", "2593", "::1", 2593)]
        public void Host_and_port_are_read(string hostText, string portText, string host, ushort port)
        {
            ShardAddress.Parse(hostText, portText, out string parsedHost, out ushort parsedPort)
                .Should().Be(ShardAddressError.None);

            parsedHost.Should().Be(host);
            parsedPort.Should().Be(port);
        }

        [Theory]
        [InlineData("", "2593")]
        [InlineData("   ", "2593")]
        [InlineData(null, "2593")]
        public void A_missing_host_is_refused(string hostText, string portText)
        {
            ShardAddress.Parse(hostText, portText, out _, out _).Should().Be(ShardAddressError.MissingHost);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("65536")]
        [InlineData("port")]
        [InlineData("")]
        public void A_port_outside_1_65535_is_refused(string portText)
        {
            ShardAddress.Parse("login.uogdemise.com", portText, out _, out ushort port)
                .Should().Be(ShardAddressError.InvalidPort);

            port.Should().Be(0);
        }
    }
}
