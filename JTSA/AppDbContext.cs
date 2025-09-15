using Microsoft.EntityFrameworkCore;
using System.IO;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        public static String dbDirectory;

        public DbSet<M_TitleText> M_TitleTextList { get; set; }
        public DbSet<M_Category> M_CategoryList { get; set; }
        public DbSet<M_Friend> M_FriendList { get; set; }
        public DbSet<M_Setting> M_SettingList { get; set; }
        public DbSet<M_TitleTag> M_TitleTagList { get; set; }
        public DbSet<M_StreamWindow> M_StreamWindowList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // AppData\Roaming\JTSA\userdata\JTSA.db
            dbDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming
                "JTSA", "userdata");
            Directory.CreateDirectory(dbDirectory); // ÉtÉHÉãÉ_Ç™Ç»ÇØÇÍÇŒçÏê¨
            var dbPath = Path.Combine(dbDirectory, "JTSA.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}