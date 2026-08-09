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
                OnPropertyChanged(nameof(StatusClass));
            }
        }
        private GameStatus status = GameStatus.None;

        // ============  ============



        public Visibility CompletedVisibility =>
            Status == GameStatus.Completed ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PlayingVisibility =>
            Status == GameStatus.Playing ? Visibility.Visible : Visibility.Collapsed;

        public string StatusClass =>
            Status == GameStatus.Completed ? " completed" :
            Status == GameStatus.Playing ? " playing" :
            "";


        // ============  ============

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
