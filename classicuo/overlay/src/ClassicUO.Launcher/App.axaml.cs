// SPDX-License-Identifier: BSD-2-Clause

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ClassicUO.Launcher
{
    public partial class App : Application
    {
        // Both set before the platform starts, which is when macOS takes them: later the
        // menu bar says "Avalonia Application", and its application menu, finding none
        // of ours, gets Avalonia's own with "About Avalonia". Other systems have no such
        // menu.
        public App()
        {
            Name = WispUO.Name;

            NativeMenuItem about = new NativeMenuItem($"About {WispUO.Name}");
            about.Click += (_, _) => AboutDialog.Show((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            NativeMenu.SetMenu(this, new NativeMenu { about });
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
