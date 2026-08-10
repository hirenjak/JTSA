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
        public int SubscribeAmount { get; set; }
    }

    public class RaidedUserForm
    {
        public string UserName { get; set; } = string.Empty;
        public int ViewerCount { get; set; }
    }
}
