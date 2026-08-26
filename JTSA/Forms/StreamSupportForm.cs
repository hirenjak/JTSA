namespace JTSA.Forms
{
    public class BitsUserForm
    {
        public string UserName { get; set; } = string.Empty;
        public int BitsAmount { get; set; }
    }

    public class SubscribeUserForm
    {
        public string UserName { get; set; } = string.Empty;
        public bool IsGift { get; set; }
        public string Tier { get; set; } = "1";
        public int CumulativeMonths { get; set; }
        public int GiftCount { get; set; }
        public string DetailText => IsGift
            ? $"Tier {Tier}のサブギフ: {GiftCount:N0}個"
            : $"累計{CumulativeMonths:N0}か月 / Tier {Tier}";
    }

    public class RaidedUserForm
    {
        public string UserName { get; set; } = string.Empty;
        public int ViewerCount { get; set; }
    }

    public class FollowUserForm
    {
        public string UserName { get; set; } = string.Empty;
        public DateTime FollowedAt { get; set; }
    }
}
