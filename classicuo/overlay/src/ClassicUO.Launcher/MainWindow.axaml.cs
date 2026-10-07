// SPDX-License-Identifier: BSD-2-Clause

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ClassicUO.Launcher
{
    public partial class MainWindow : Window
    {
        private readonly LauncherConfig _config;
        private readonly ClientSettings _settings;
        private readonly ObservableCollection<Shard> _shards;

        public MainWindow()
        {
            InitializeComponent();

            Title = WispUO.NameAndVersion;
            AboutText.Text = AboutDialog.PoweredBy;

            _config = LauncherConfig.Load();
            _settings = ClientSettings.Load();

            // First run: the shard the client was already pointed at becomes the first entry.
            if (_config.Shards.Count == 0 && !string.IsNullOrWhiteSpace(_settings.Ip))
            {
                _config.Shards.Add
                (
                    new Shard
                    {
                        Name = _settings.Ip,
                        Host = _settings.Ip,
                        Port = _settings.Port
                    }
                );

                _config.LastShard = _settings.Ip;
            }

            // Game folder and client version used to be one for every shard, kept in
            // settings.json: shards saved back then take them over.
            foreach (Shard shard in _config.Shards.Where(s => string.IsNullOrEmpty(s.UODirectory)))
            {
                shard.UODirectory = _settings.UltimaOnlineDirectory;
                shard.ClientVersion = _settings.ClientVersion;
            }

            // The account the client saved during the last session belongs to the shard it
            // was played on: remember it there, so Play can put it back for that shard.
            Shard lastPlayed = _config.Shards.FirstOrDefault(s => s.Name == _config.LastShard);

            if (lastPlayed != null && _settings.PointsAt(lastPlayed))
            {
                lastPlayed.Username = _settings.Username;
                lastPlayed.Password = _settings.Password;
                lastPlayed.SaveAccount = _settings.SaveAccount;
            }

            _shards = new ObservableCollection<Shard>(_config.Shards);
            ShardList.ItemsSource = _shards;
            ShardList.SelectedItem = _shards.FirstOrDefault(s => s.Name == _config.LastShard) ?? _shards.FirstOrDefault();

            UpdateButtons();
        }

        private Shard SelectedShard => ShardList.SelectedItem as Shard;

        private void UpdateButtons()
        {
            bool hasSelection = SelectedShard != null;

            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
            PlayButton.IsEnabled = hasSelection;
            EmptyListText.IsVisible = _shards.Count == 0;
        }

        private void SaveShards()
        {
            _config.Shards = _shards.ToList();

            try
            {
                _config.Save();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Cannot save the shard list: {ex.Message}";
            }
        }

        private IEnumerable<string> NamesExcept(Shard shard)
        {
            return _shards.Where(s => s != shard).Select(s => s.Name);
        }

        private void OnShardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StatusText.Text = string.Empty;
            UpdateButtons();
        }

        private void OnShardDoubleTapped(object sender, TappedEventArgs e)
        {
            if (SelectedShard != null)
            {
                OnPlay(sender, e);
            }
        }

        private async void OnAddShard(object sender, RoutedEventArgs e)
        {
            Shard shard = await new ShardWindow(null, NamesExcept(null), SelectedShard ?? _shards.FirstOrDefault()).ShowDialog<Shard>(this);

            if (shard == null)
            {
                return;
            }

            _shards.Add(shard);
            ShardList.SelectedItem = shard;
            SaveShards();
            UpdateButtons();
        }

        private async void OnEditShard(object sender, RoutedEventArgs e)
        {
            Shard current = SelectedShard;

            if (current == null)
            {
                return;
            }

            Shard edited = await new ShardWindow(current, NamesExcept(current)).ShowDialog<Shard>(this);

            if (edited == null)
            {
                return;
            }

            // Replaced rather than mutated: Shard does not notify, the list redraws the new item.
            int index = _shards.IndexOf(current);
            _shards[index] = edited;

            if (_config.LastShard == current.Name)
            {
                _config.LastShard = edited.Name;
            }

            ShardList.SelectedItem = edited;
            SaveShards();
        }

        private async void OnDeleteShard(object sender, RoutedEventArgs e)
        {
            Shard shard = SelectedShard;

            if (shard == null)
            {
                return;
            }

            bool confirmed = await ConfirmDialog.Ask(this, "Delete shard", $"Delete \"{shard.Name}\"?", "Delete");

            if (!confirmed)
            {
                return;
            }

            int index = _shards.IndexOf(shard);
            _shards.Remove(shard);

            if (_shards.Count > 0)
            {
                ShardList.SelectedItem = _shards[Math.Min(index, _shards.Count - 1)];
            }

            SaveShards();
            UpdateButtons();
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            Shard shard = SelectedShard;

            if (shard == null)
            {
                StatusText.Text = "Select a shard first.";

                return;
            }

            if (!ClientSettings.IsValidUODirectory(shard.UODirectory))
            {
                StatusText.Text = string.IsNullOrWhiteSpace(shard.UODirectory)
                    ? $"Choose the Ultima Online folder of \"{shard.Name}\": edit the shard."
                    : $"The game folder of \"{shard.Name}\" is not an Ultima Online folder: edit the shard.";

                return;
            }

            if (ClientStarter.FindClient() == null)
            {
                StatusText.Text = $"The ClassicUO client is missing from {ClientStarter.BinDirectory}";

                return;
            }

            try
            {
                _settings.Apply(shard);
                _settings.Save();

                _config.LastShard = shard.Name;
                SaveShards();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Cannot write settings.json: {ex.Message}";

                return;
            }

            Program.PlayRequested = true;
            Close();
        }

        private void OnQuit(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
