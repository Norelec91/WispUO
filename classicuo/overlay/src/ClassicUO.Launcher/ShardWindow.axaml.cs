// SPDX-License-Identifier: BSD-2-Clause

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Launcher
{
    /// <summary>
    ///     Adds or edits a shard. Closes with the resulting <see cref="Shard"/>, or with
    ///     null when cancelled; the shard passed in is never modified.
    /// </summary>
    public partial class ShardWindow : Window
    {
        private readonly HashSet<string> _takenNames;
        private readonly Shard _original;

        // for the XAML previewer
        public ShardWindow() : this(null, Array.Empty<string>())
        {
        }

        /// <param name="gameFilesFrom">
        ///     For a new shard, whose game folder and client version to start from: most
        ///     shards are played with the same files.
        /// </param>
        public ShardWindow(Shard shard, IEnumerable<string> takenNames, Shard gameFilesFrom = null)
        {
            InitializeComponent();

            _takenNames = new HashSet<string>(takenNames, StringComparer.OrdinalIgnoreCase);
            _original = shard;

            Title = shard == null ? "Add shard" : "Edit shard";

            NameBox.Text = shard?.Name;
            HostBox.Text = shard?.Host;
            PortBox.Text = (shard?.Port ?? 2593).ToString();
            UOFolderBox.Text = (shard ?? gameFilesFrom)?.UODirectory;
            ClientVersionBox.Text = (shard ?? gameFilesFrom)?.ClientVersion;

            ClassicAssistBox.IsChecked = shard?.ClassicAssist == true;

            if (!ClientSettings.ClassicAssistInstalled)
            {
                ClassicAssistBox.IsEnabled = false;
                ClassicAssistBox.IsChecked = false;
                ClassicAssistHint.Text = "ClassicAssist is not installed: it goes in Data/Plugins/ClassicAssist.";
            }

            EncryptionBox.IsChecked = shard?.Encryption == true;

            // what is set there stays in view
            AdvancedExpander.IsExpanded = ClassicAssistBox.IsChecked == true
                || !string.IsNullOrEmpty(ClientVersionBox.Text)
                || EncryptionBox.IsChecked == true;

            Opened += (_, _) => NameBox.Focus();
        }

        private async void OnBrowse(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync
            (
                new FolderPickerOpenOptions
                {
                    Title = "Ultima Online folder",
                    AllowMultiple = false
                }
            );

            string path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;

            if (!string.IsNullOrEmpty(path))
            {
                UOFolderBox.Text = path;
                ErrorText.Text = ClientSettings.IsValidUODirectory(path) ? string.Empty : "That is not an Ultima Online folder.";
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text?.Trim() ?? string.Empty;

            switch (ShardAddress.Parse(HostBox.Text, PortBox.Text, out string host, out ushort port))
            {
                case ShardAddressError.MissingHost:
                    ShowError("Enter the host of the shard.", HostBox);

                    return;

                case ShardAddressError.InvalidPort:
                    ShowError("The port must be a number between 1 and 65535.", PortBox);

                    return;
            }

            string uoDirectory = UOFolderBox.Text?.Trim() ?? string.Empty;

            if (!ClientSettings.IsValidUODirectory(uoDirectory))
            {
                ShowError("Choose the Ultima Online folder.", UOFolderBox);

                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = host;
            }

            if (_takenNames.Contains(name))
            {
                ShowError($"There is already a shard called \"{name}\".", NameBox);

                return;
            }

            Close
            (
                new Shard
                {
                    Name = name,
                    Host = host,
                    Port = port,
                    UODirectory = uoDirectory,
                    ClientVersion = ClientVersionBox.Text?.Trim() ?? string.Empty,
                    ClassicAssist = ClassicAssistBox.IsChecked == true,
                    Encryption = EncryptionBox.IsChecked == true,

                    // not edited here: the account last used on the shard carries over
                    Username = _original?.Username ?? string.Empty,
                    Password = _original?.Password ?? string.Empty,
                    SaveAccount = _original?.SaveAccount ?? true
                }
            );
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void ShowError(string message, Control field)
        {
            ErrorText.Text = message;
            field.Focus();
        }
    }
}
