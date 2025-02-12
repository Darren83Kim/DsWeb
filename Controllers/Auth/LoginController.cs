using DsWebServer.Managers;
using DsWebServer.Model;
using DsWebServer.Packets;
using DsWebServer.Model;

namespace DsWebServer.Controllers
{
    public class LoginController : BaseController<ReqLogin, ResLogin>
    {
        public override async Task<ResLogin> Process(ReqLogin request)
        {
            var response = new ResLogin();

            // DB 확인.
            var userInfo = await GameDBManager.SelectUserInfo(request.userName);
            if (userInfo == null)
                return SendResponse(response, ResultCode.UserNotFindDBInfo);

            // 패스워드 확인.
            if (userInfo.UserPass != request.userPass)
                return SendResponse(response, ResultCode.UserPassNotMatch);

            // 이미 로그인 되어 있는지 확인.
            if (userInfo.Token != null)
            {
                var existingSession = await RedisManager.GetSessionAsync(userInfo.Token);
                if (!string.IsNullOrEmpty(existingSession))
                    await RedisManager.DeleteSessionAsync(userInfo.Token);
            }

            var token = Guid.NewGuid().ToString();
            await RedisManager.SetSessionAsync(token, request.userName, TimeSpan.FromMinutes(30));

            response.token = token;

            return SendResponse(response);
        }
    }
}
