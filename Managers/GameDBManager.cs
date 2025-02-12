using Dapper;
using DsWebServer.Model;
using System.Data;

namespace DsWebServer.Managers
{
    public class GameDBManager : DBManager
    {
        public static async Task<UserDBInfo?> SelectUserInfo(string userName)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@userName", userName, DbType.String);

            return await ExecuteQuery<UserDBInfo>("SELECT_USER_INFO", parameters);
        }

        public static async Task<bool> InsertUserInfo(string userName, string userPass, int charType)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@userName", userName, DbType.String);
            parameters.Add("@userPass", userPass, DbType.String);
            parameters.Add("@charType", charType, DbType.Int32);

            return await ExecuteQuery("INSERT_USER_INFO", parameters);
        }
    }
}
