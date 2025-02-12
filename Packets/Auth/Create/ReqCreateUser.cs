namespace DsWebServer.Packets
{
    public class ReqCreateUser : BaseRequest
    {
        public ReqCreateUser() : base("api/CreateUser") { }

        public string userName { get; set; }
        public string userPass { get; set; }
        public int charType { get; set; }
    }
}
