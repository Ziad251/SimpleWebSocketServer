using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketServer.Services;
using Serilog;


namespace WebSocketServer.Middleware
{
    public class WebSocketServerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly WebSocketServerConnectionManager _manager;


        public WebSocketServerMiddleware(RequestDelegate next, WebSocketServerConnectionManager manager)
        {
            _next = next;
            _manager = manager;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            
                // Summary: We check the context object's “WebSockets” property to establish a new websocket connection. 
                // If this is an upgrade request to establish a new WebSocket connection we accept.
            if (context.WebSockets.IsWebSocketRequest)
            {
                
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                string ConnID = _manager.AddSocket(webSocket);
                
                await SendConnIDAsync(webSocket, ConnID); 

                Log.Information("WebSocket Connected");


                // Summary: We recieve the Json text messages from the connected socket and route it to our users
                await Receive(webSocket, async (result, buffer) =>
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string data = Encoding.UTF8.GetString(buffer, 0, result.Count);
                       
                       
                        Log.Information($"Received->Text");
                        Log.Information($"Message: {data}");
                        
                        
                        await RouteJSONMessageAsync(data);
                        return;
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        string id = _manager.GetAllSockets().FirstOrDefault(s => s.Value == webSocket).Key;
                        Log.Information($"Received->Close");

                        _manager.GetAllSockets().TryRemove(id, out WebSocket sock);
                        Log.Information($"Managed Connections: {_manager.GetAllSockets().Count.ToString()}");

                        await sock.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    }
                });
            }

            else
            {
                await _next(context);
            }   
        }
        // Summary: Receive method enters a while loop that remains true while we have an open connection on our WebSocket, 
        // When we eventually receive a message, (send method on our API), ReceiveAsync fires, calling our Action Delegate: handleMessage.
        private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 4];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer),
                                                       cancellationToken: CancellationToken.None);

                handleMessage(result, buffer);
            }
        }

        // Summary: We send back a Guid(ConnID) we created for the new client(socket) so we can route their message accordingly 
        private async Task SendConnIDAsync(WebSocket socket, string connID)
        {
        var buffer = Encoding.UTF8.GetBytes($"ConnID: {connID}");
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        // Summary: the receieved Json is asynchronously sent to to the rest of the connected open sockets
        private async Task RouteJSONMessageAsync(string message)
        {

        var routeOb = JsonConvert.DeserializeObject<dynamic>(message);

        string user = routeOb.Nickname.ToString();
        string userID = routeOb.From.ToString();
        string recipientUser = routeOb.To.ToString();
        _manager.AddUser(user, userID);

        
        if (recipientUser != null)
        {
            Log.Information("Targeted");
            var recipient = _manager.GetAllSockets().Where(u => u.Key == recipientUser).Select(u => u.Value).FirstOrDefault();            
            if (recipient != null)
            {
            if (recipient.State == WebSocketState.Open)
                await recipient.SendAsync(Encoding.UTF8.GetBytes(routeOb.Message.ToString()), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
            Log.Information("Invalid recipient.");
            }
        }
         else
         {
             Log.Information("No recipient given.");
            
         }
        }
    }
}