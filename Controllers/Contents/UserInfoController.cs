using DsWebServer.Managers;
using DsWebServer.Model;
using DsWebServer.Packets;

namespace DsWebServer.Controllers
{
    public class UserInfoController : SessionController<ReqUserInfo, ResUserInfo>
    {
        public override async Task<ResUserInfo> Process(ReqUserInfo request)
        {
            var response = new ResUserInfo();

            var userInfo = await GameDBManager.SelectUserInfo(userName);
            if (userInfo == null)
                return SendResponse(response, ResultCode.UserNotFindDBInfo);

            response.userName = userInfo.UserName;
            response.charType = userInfo.CharType;
            response.userPoint = userInfo.UserPoint;
            response.maxScore = userInfo.MaxScore;
            response.latestDate = userInfo.LetestDate;
            response.createDate = userInfo.CreateDate;

            return SendResponse(response);

        }
    }
}
