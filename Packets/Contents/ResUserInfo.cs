namespace DsWebServer.Packets
{
    public class ResUserInfo : BaseResponse
    {
        public string userName { get; set; }
        public int charType { get; set; }
        public int userPoint { get; set; }
        public int maxScore { get; set; }
        public DateTime latestDate { get; set; }
        public DateTime createDate { get; set; }
    }
}
