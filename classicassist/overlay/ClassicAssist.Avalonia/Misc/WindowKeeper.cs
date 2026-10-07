using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ClassicAssist.Avalonia.Misc;

/// <summary>
///     The assistant lives as long as the game, which ends it: closing its main window, or
///     quitting it from the macOS menu, only puts the window away. On macOS it hides and comes
///     back from the Dock icon or the application menu; elsewhere it goes to the taskbar.
/// </summary>
public static class WindowKeeper
{
    private static Window _main;

    /// <summary>
    ///     Called from the App constructor: macOS takes the application menu when the platform
    ///     starts, and finding none of ours then, Avalonia puts its own with "About Avalonia".
    /// </summary>
    public static void InstallMenu( Application application )
    {
        if ( !OperatingSystem.IsMacOS() )
        {
            return;
        }

        NativeMenuItem show = new( "Show ClassicAssist" );
        show.Click += ( _, _ ) =>
        {
            if ( _main != null )
            {
                Bring( _main );
            }
        };

        NativeMenu.SetMenu( application, new NativeMenu { show } );
    }

    public static void Install( IClassicDesktopStyleApplicationLifetime desktop, Window main )
    {
        _main = main;

        main.Closing += ( _, e ) =>
        {
            // the game closing ends the lifetime, which closes the window for another reason
            if ( e.CloseReason != WindowCloseReason.WindowClosing )
            {
                return;
            }

            e.Cancel = true;
            PutAway( main );
        };

        if ( !OperatingSystem.IsMacOS() )
        {
            return;
        }

        // Quit in the application menu, or Cmd+Q
        desktop.ShutdownRequested += ( _, e ) =>
        {
            e.Cancel = true;
            PutAway( main );
        };

        // a click on the Dock icon
        if ( Application.Current?.TryGetFeature( typeof( IActivatableLifetime ) ) is IActivatableLifetime activatable )
        {
            activatable.Activated += ( _, e ) =>
            {
                if ( e.Kind == ActivationKind.Reopen )
                {
                    Bring( main );
                }
            };
        }
    }

    private static void PutAway( Window window )
    {
        if ( OperatingSystem.IsMacOS() )
        {
            window.Hide();
        }
        else
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private static void Bring( Window window )
    {
        window.Show();

        if ( window.WindowState == WindowState.Minimized )
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
