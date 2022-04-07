using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using Serilog;

namespace WebSocketServer.Services
{
    public class WebSocketServerConnectionManager
    {
        
        private ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();
        private ConcurrentDictionary<string, string> _usernames = new ConcurrentDictionary<string, string>();

        // Summary: Adds socket to our sockets dictionary and return a Guid String ConnID
        public string AddSocket(WebSocket socket)
        {
            string ConnID = Guid.NewGuid().ToString();
            _sockets.TryAdd(ConnID, socket);

            Log.Information($"WebSocketServerConnectionManager-> AddSocket: WebSocket added with ID: {ConnID}");
            return ConnID;
        }

         public string AddUser(string username, string ConnID)
         {

            _usernames.TryAdd(username, ConnID);
            Log.Information($"WebSocketServerConnectionManager-> Username: {username} was added to dictionary with ID: {ConnID}");
            return username;   
         }

        public ConcurrentDictionary<string, WebSocket> GetAllSockets()
        {
            return _sockets;
        }

        public ConcurrentDictionary<string, string> GetAllUsers()
        {
            return _usernames;
        }

    }
}