using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SuperSocket.ClientEngine;
using Valsom.VoiceAttack;
using Valsom.VoiceAttack.Log;
using WebSocket4Net;

namespace EliteVA
{
    public class VoiceAttackPlugin
    {
        public static Guid VA_Id() => new Guid("189a4e44-caf1-459b-b62e-fabc60a12986");

        public static string VA_DisplayName() => "EliteVA";

        public static string VA_DisplayInfo() => "EliteVA by Somfic";

        private static WebSocket _socket;
        private static VoiceAttackProxy _proxy;

        public static void VA_Init1(dynamic vaProxy)
        {
            _proxy = new VoiceAttackProxy(vaProxy);
            _socket = new WebSocket("ws://localhost:8001/ws", "EliteAPI");
            
            _socket.Opened += SocketOpened;
            _socket.Error += SocketError;
            _socket.Closed += SocketClosed;
            _socket.MessageReceived += SocketMessage;
            
            _socket.Open();
        }

        private static void SocketMessage(object sender, MessageReceivedEventArgs e)
        {
            ReceivedMessage(JsonConvert.DeserializeObject<WebSocketMessage>(e.Message));
        }

        private static void SocketClosed(object sender, EventArgs e)
        {
            Log("The connection to EliteAPI Hub has closed", VoiceAttackColor.Yellow);
        }

        private static void SocketError(object sender, ErrorEventArgs e)
        {
            Log($"Could not connect to EliteAPI Hub ({e.Exception.GetType().Name}: {e.Exception})", VoiceAttackColor.Red);
        }

        private static void SocketOpened(object sender, EventArgs e)
        {
            Log("Authenticating with EliteAPI Hub", VoiceAttackColor.Gray);
            SendMessage(new WebSocketMessage("auth", "client"));
        }

        public static void VA_Exit1(dynamic vaProxy)
        {
            _proxy = new VoiceAttackProxy(vaProxy);
        }

        public static void VA_StopCommand()
        {
        }

        public static void VA_Invoke1(dynamic vaProxy)
        {
            _proxy = new VoiceAttackProxy(vaProxy);
        }

        private static void SendMessage(WebSocketMessage message)
        {
            _socket.Send(JsonConvert.SerializeObject(message));
        }

        private static void ReceivedMessage(WebSocketMessage message)
        {
            dynamic value;

            try
            {
                value = JsonConvert.DeserializeObject(message.Value);
            }
            catch (Exception ex)
            {
                value = message.Value;
            }

            switch (message.Type)
            {
                case "EliteAPI":
                    Log($"Connection established with EliteAPI v{value.Version}", VoiceAttackColor.Green);
                    break;

                case "CatchupStart":
                    Log($"Catching up on {value} events in this session", VoiceAttackColor.Gray);
                    break;

                case "CatchupEnd":
                    Log($"Caught up with previous events", VoiceAttackColor.Green);
                    break;

                default:
                    Log($"[{message.Type}] {value}", VoiceAttackColor.Yellow);
                    break;
            }
        }

        private static void Log(string message, VoiceAttackColor color = VoiceAttackColor.Purple)
        {
            _proxy.Log.Write($"[EliteAPI] {message}", color);
        }
    }
}