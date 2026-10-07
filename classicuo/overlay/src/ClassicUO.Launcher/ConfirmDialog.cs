// SPDX-License-Identifier: BSD-2-Clause

using Avalonia.Controls;
using Avalonia.Layout;
using System.Threading.Tasks;

namespace ClassicUO.Launcher
{
    /// <summary>A yes/no question: Avalonia has no message box of its own.</summary>
    internal static class ConfirmDialog
    {
        public static Task<bool> Ask(Window owner, string title, string message, string confirmText)
        {
            Window dialog = new Window
            {
                Title = title,
                Icon = owner.Icon,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            Button cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            Button confirm = new Button { Content = confirmText, IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            confirm.Classes.Add("accent");

            cancel.Click += (_, _) => dialog.Close(false);
            confirm.Click += (_, _) => dialog.Close(true);

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, confirm }
                    }
                }
            };

            return dialog.ShowDialog<bool>(owner);
        }
    }
}
