using JTSA.Panels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static JTSA.Dao.DAO_GamePlaylist;

namespace JTSA.Forms
{
    public class PlaylistItemForm : INotifyPropertyChanged
    {
        /// <summary> カテゴリーID </summary>
        public long PlaylistId { get; set; }
        public bool IsReadOnly { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
        public Visibility DeleteVisibility => IsReadOnly ? Visibility.Collapsed : Visibility.Visible;

        /// <summary> カテゴリーID </summary>
        public string CategoryId { get; set; } = "";

        /// <summary> イメージURL </summary>
        public string ImageUrl { get; set; } = "";

        /// <summary> プレイ中ステータス </summary>
        public GameStatus Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(CompletedVisibility));
                OnPropertyChanged(nameof(PlayingVisibility));
                OnPropertyChanged(nameof(InterruptedVisibility));
                OnPropertyChanged(nameof(StatusClass));
            }
        }
        private GameStatus status = GameStatus.None;

        // ============  ============



        public Visibility CompletedVisibility =>
            Status == GameStatus.Completed ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PlayingVisibility =>
            Status == GameStatus.Playing ? Visibility.Visible : Visibility.Collapsed;

        public Visibility InterruptedVisibility =>
            Status == GameStatus.Interrupted ? Visibility.Visible : Visibility.Collapsed;

        public string StatusClass =>
            Status == GameStatus.Completed ? " completed" :
            Status == GameStatus.Playing ? " playing" :
            Status == GameStatus.Interrupted ? " interrupted" :
            "";


        // ============  ============

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
