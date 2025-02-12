using DsWebServer.Model;
using DsWebServer.Packets;
using System.Text.Json;

namespace DsWebServer.Controllers
{
    public abstract class BaseController<TRequest, TResponse>
        where TRequest : BaseRequest
        where TResponse : BaseResponse, new()
    {
        public abstract Task<TResponse> Process(TRequest request);

        public TResponse SendResponse(TResponse response, ResultCode resultCode = ResultCode.Success)
        {
            response.resultCode = (int)resultCode;
            return response;
        }

        public async Task HandleRequest(HttpContext context)
        {
            try
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<TRequest>(requestBody);

                if (request == null)
                {
                    var errorResponse = new TResponse
                    {
                        resultCode = -1
                    };
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(errorResponse);
                    return;
                }

                //if (request.token != null && request.token.Length > 0)
                //{
                //    if (this is SessionController<TRequest, TResponse> sessionController)
                //    {
                //        var sessionResponse = await sessionController.HandleRequest(request);
                //        await context.Response.WriteAsJsonAsync(sessionResponse);
                //        return;
                //    }
                //}

                var response = await Process(request);
                await context.Response.WriteAsJsonAsync(response);
                
            }
            catch (Exception ex)
            {
                var errorResponse = new TResponse
                {
                    resultCode = -1
                };
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(errorResponse);
            }
        }
    }
}
