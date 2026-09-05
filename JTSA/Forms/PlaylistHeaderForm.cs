using JTSA.Models;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JTSA.Forms
{
    public class PlaylistHeaderForm : INotifyPropertyChanged
    {
        public bool IsReadOnly { get; set; }
        public Visibility DeleteVisibility => IsReadOnly ? Visibility.Collapsed : Visibility.Visible;
        private string gamePlayListName = string.Empty;

        public required long GamePlayListId { get; set; }
        public required string GamePlayListName
        {
            get => gamePlayListName;
            set
            {
                if (gamePlayListName == value) return;
                gamePlayListName = value;
                OnPropertyChanged();
            }
        }
        public required string LastUsedDate { get; set; }
        public required string ImageUrl { get; set; }
        public required bool IsLoaded { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
