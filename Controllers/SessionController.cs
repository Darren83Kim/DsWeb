using DsWebServer.Managers;
using DsWebServer.Model;
using DsWebServer.Packets;
using System.Text.Json;

namespace DsWebServer.Controllers
{
    public abstract class SessionController<TRequest, TResponse>
        where TRequest : BaseRequest
        where TResponse : BaseResponse, new()
        //public class SessionController<TRequest, TResponse> : BaseController<TRequest, TResponse>
        //            where TRequest : BaseRequest
        //            where TResponse : BaseResponse, new()
    {
        protected string userName { get; set; } = string.Empty;

        public abstract Task<TResponse> Process(TRequest request);

        public TResponse SendResponse(TResponse response, ResultCode resultCode = ResultCode.Success)
        {
            response.resultCode = (int)resultCode;
            return response;
        }

        public async Task SendError(HttpContext context, ResultCode retCode)
        {
            var errorResponse = new TResponse
            {
                resultCode = (int)retCode
            };
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(errorResponse);
        }

        public async Task HandleRequest(HttpContext context)
        {
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<TRequest>(requestBody);

            if (request == null || request.token == null)
            {
                await SendError(context, ResultCode.Fail);
                return;
            }

            var response = new TResponse();
            try
            {
                // Redis에서 value를 가져와서 userName에 세팅
                var redisValue = await RedisManager.GetSessionAsync(request.token);
                if (string.IsNullOrEmpty(redisValue))
                {
                    await SendError(context, ResultCode.Fail);
                    return;
                }

                if (!await RedisManager.SessionLockTake(request.token))
                {
                    await SendError(context, ResultCode.Fail);
                    return;
                }

                userName = redisValue;
                response = await Process(request);
                await context.Response.WriteAsJsonAsync(response);
            }
            finally
            {
                // 락 해제
                await RedisManager.SessionLockRelease(request.token);
            }
        }
    }
}
