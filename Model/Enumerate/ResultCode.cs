namespace DsWebServer.Model
{
    public enum ResultCode
    {
        Success = 0,
        Fail = -1,

        DBError = -10,

        UserAlreadyLoggedIn = -100,
        UserNotFindDBInfo = -101,
        UserAlreadyExistInfo = -102,
        UserPassNotMatch = -103,
    }
}
