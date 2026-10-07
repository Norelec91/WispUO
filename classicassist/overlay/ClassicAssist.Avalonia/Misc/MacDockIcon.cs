using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace ClassicAssist.Avalonia.Misc;

/// <summary>
///     macOS takes the Dock icon from the application bundle, and this process has none:
///     Avalonia does not pass Window.Icon on, so the icon is handed to NSApplication here.
/// </summary>
internal static class MacDockIcon
{
    private const string OBJC = "/usr/lib/libobjc.A.dylib";

    public static void Set( string asset )
    {
        if ( !OperatingSystem.IsMacOS() )
        {
            return;
        }

        try
        {
            byte[] bytes;

            using ( Stream stream = AssetLoader.Open( new Uri( asset ) ) )
            using ( MemoryStream memory = new() )
            {
                stream.CopyTo( memory );
                bytes = memory.ToArray();
            }

            IntPtr data = SendBytes( objc_getClass( "NSData" ), sel_registerName( "dataWithBytes:length:" ), bytes,
                (nuint) bytes.Length );
            IntPtr image = Send( Send( objc_getClass( "NSImage" ), sel_registerName( "alloc" ) ),
                sel_registerName( "initWithData:" ), data );
            IntPtr application = Send( objc_getClass( "NSApplication" ), sel_registerName( "sharedApplication" ) );

            if ( image != IntPtr.Zero )
            {
                Send( application, sel_registerName( "setApplicationIconImage:" ), image );
            }
        }
        catch ( Exception )
        {
            // only cosmetic: the Dock keeps its generic icon
        }
    }

    [DllImport( OBJC )]
    private static extern IntPtr objc_getClass( string name );

    [DllImport( OBJC )]
    private static extern IntPtr sel_registerName( string name );

    [DllImport( OBJC, EntryPoint = "objc_msgSend" )]
    private static extern IntPtr Send( IntPtr receiver, IntPtr selector );

    [DllImport( OBJC, EntryPoint = "objc_msgSend" )]
    private static extern IntPtr Send( IntPtr receiver, IntPtr selector, IntPtr argument );

    [DllImport( OBJC, EntryPoint = "objc_msgSend" )]
    private static extern IntPtr SendBytes( IntPtr receiver, IntPtr selector, byte[] bytes, nuint length );
}
