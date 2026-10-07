// SPDX-License-Identifier: BSD-2-Clause

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ClassicUO.Launcher
{
    /// <summary>What WispUO is, for the About item of the macOS application menu.</summary>
    internal static class AboutDialog
    {
        public const string PoweredBy = "Powered by ClassicUO and ClassicAssist";

        public static void Show(Window owner)
        {
            Window dialog = new Window
            {
                Title = $"About {WispUO.Name}",
                Icon = owner?.Icon,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
            };

            Button ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (_, _) => dialog.Close();

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = WispUO.NameAndVersion, FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = PoweredBy, Opacity = 0.7 },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Avalonia.Thickness(0, 8, 0, 0),
                        Children = { ok }
                    }
                }
            };

            if (owner != null)
            {
                dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }
        }
    }
}
