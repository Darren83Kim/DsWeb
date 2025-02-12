namespace DsWebServer.Model
{
    public class UserDBInfo
    {
        public long UserSeq { get; set; }
        public string Token { get; set; }
        public string UserName { get; set; }
        public string UserPass { get; set; }
        public int CharType { get; set; }
        public int UserPoint { get; set; }
        public int MaxScore { get; set; }
        public DateTime LetestDate { get; set; } = DateTime.Now;
        public DateTime CreateDate { get; set; }
    }
}
