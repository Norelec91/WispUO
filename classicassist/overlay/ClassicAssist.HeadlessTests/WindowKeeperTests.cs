using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClassicAssist.Avalonia.Misc;
using Xunit;

namespace ClassicAssist.HeadlessTests;

/// <summary>
///     WispUO: the assistant lives as long as the game, so closing its main window puts the window
///     away instead of closing it (WindowKeeper). These run on Windows and Linux, where it minimizes.
/// </summary>
public class WindowKeeperTests
{
    [Fact]
    public Task Closing_the_main_window_minimizes_it_and_keeps_it_open()
    {
        return Headless.Run( () =>
        {
            if ( OperatingSystem.IsMacOS() )
            {
                return; // there it hides instead, which a headless window cannot show
            }

            Window window = new();
            WindowKeeper.Install( new ClassicDesktopStyleApplicationLifetime(), window );
            window.Show();

            window.Close();
            Headless.Settle();

            Assert.True( window.IsVisible );
            Assert.Equal( WindowState.Minimized, window.WindowState );
        } );
    }

    [Fact]
    public Task Other_windows_close_as_usual()
    {
        return Headless.Run( () =>
        {
            Window main = new();
            WindowKeeper.Install( new ClassicDesktopStyleApplicationLifetime(), main );

            Window other = new();
            bool closed = false;
            other.Closed += ( _, _ ) => closed = true;
            other.Show();

            other.Close();
            Headless.Settle();

            Assert.True( closed );
        } );
    }
}
