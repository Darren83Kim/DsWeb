using DsWebServer.Managers;
using DsWebServer.Model;
using DsWebServer.Packets;
using DsWebServer.Utils;

namespace DsWebServer.Controllers
{
    public class CreateUserController : BaseController<ReqCreateUser, ResCreateUser>
    {
        public override async Task<ResCreateUser> Process(ReqCreateUser request)
        {
            var response = new ResCreateUser();
            var existingSession = await RedisManager.GetSessionAsync(request.userName);
            if (!string.IsNullOrEmpty(existingSession))
                return SendResponse(response, ResultCode.UserAlreadyLoggedIn);

            var userInfo = await GameDBManager.SelectUserInfo(request.userName);
            if (userInfo == null)
            {
                if (!await GameDBManager.InsertUserInfo(request.userName, request.userPass, request.charType))
                    throw new GameException(ResultCode.Fail, "Failed to insert user info");

                //userInfo = await GameDBManager.SelectUserInfo(request.userName);
            }
            else
                return SendResponse(response, ResultCode.UserAlreadyExistInfo);


            //var token = Guid.NewGuid().ToString();
            //await RedisManager.SetSessionAsync(request.userName, token, TimeSpan.FromMinutes(30));
            return SendResponse(response);
        }
    }
}
