using DsWebServer.Managers;
using DsWebServer.Packets;

namespace DsWebServer.Controllers
{
    public class LogOutController : SessionController<ReqLogOut, ResLogOut>
    {
        public override async Task<ResLogOut> Process(ReqLogOut request)
        {
            var response = new ResLogOut();
            await RedisManager.DeleteSessionAsync(request.token);
            return SendResponse(response);
        }
    }
}
