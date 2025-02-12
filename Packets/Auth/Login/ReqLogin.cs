namespace DsWebServer.Packets
{
    public class ReqLogin : BaseRequest
    {
        public ReqLogin() : base("api/Login") { }

        public string userName { get; set; }
        public string userPass { get; set; }
    }
}
