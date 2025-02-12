using Azure.Core;
using Azure;
using DsWebServer.Model;
using StackExchange.Redis;

namespace DsWebServer.Managers
{
    public class RedisManager
    {
        private static IConnectionMultiplexer _redis;

        public static void Initialize(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public static async Task<string> GetSessionAsync(string key)
        {
            var db = _redis.GetDatabase();
            return await db.StringGetAsync(key);
        }

        public static async Task SetSessionAsync(string key, string value, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value, expiry);
        }

        public static async Task DeleteSessionAsync(string key)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }

        public static async Task<bool> SessionLockTake(string token)
        {
            var lockKey = $"lock:{token}";
            var lockValue = token;

            return await _redis.GetDatabase().LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(30));
        }

        public static async Task<bool> SessionLockRelease(string token)
        {
            var lockKey = $"lock:{token}";
            var lockValue = token;
            return await _redis.GetDatabase().LockReleaseAsync(lockKey, lockValue);
        }
    }
}
