using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EliteVA.Models;
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

        private static int _maxReconnectionAttempts = 3;
        private static int _reconnectionAttempts = 0;

        private static bool _hasCaughtUp = false;

        public static void VA_Init1(dynamic vaProxy)
        {
            _proxy = new VoiceAttackProxy(vaProxy);
            _socket = new WebSocket("ws://localhost:8001/ws", "EliteAPI-plugin");

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
            Log("The connection to EliteAPI Hub has closed", VoiceAttackColor.Red);

            if (_reconnectionAttempts == _maxReconnectionAttempts)
            {
                Log(
                    "Could not establish a connection to EliteAPI Hub. EliteAPI will not be functional. Restart VoiceAttack to try again",
                    VoiceAttackColor.Purple);
                return;
            }

            _reconnectionAttempts++;
            Thread.Sleep(3000);
            Log($"Trying to reconnect to EliteAPI Hub ({_reconnectionAttempts}/{_maxReconnectionAttempts}) ... ",
                VoiceAttackColor.Gray);
            _socket.Open();
        }

        private static void SocketError(object sender, ErrorEventArgs e)
        {
            Log($"Could not connect to EliteAPI Hub ({e.Exception.GetType().Name}: {e.Exception})",
                VoiceAttackColor.Purple);
        }

        private static void SocketOpened(object sender, EventArgs e)
        {
            _reconnectionAttempts = 0;
            Log("Authenticating with EliteAPI Hub", VoiceAttackColor.Gray);
            SendMessage(new WebSocketMessage("auth", "plugin"));
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
                    _hasCaughtUp = false;
                    break;

                case "CatchupEnd":
                    Log($"Caught up with previous events", VoiceAttackColor.Green);
                    _hasCaughtUp = true;
                    break;

                case "Cargo":
                case "Modules":
                case "Outfitting":
                case "Shipyard":
                case "Status":
                case "NavRoute":
                case "Market":
                case "Backpack":
                case "Event":
                    try
                    {
                        var eventValue = JsonConvert.DeserializeObject<Event>(message.Value);
                        eventValue.Variables.ForEach(SetVariable);
                        if (_hasCaughtUp)
                        {
                            Log($"Invoking ((EliteAPI.{eventValue.Name}))", VoiceAttackColor.Pink);
                            _proxy.Commands.Invoke($"((EliteAPI.{eventValue.Name}))");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"{ex.GetType().Name}: {ex.Message}");
                    }

                    break;

                default:
                    Log($"[{message.Type}] {value}", VoiceAttackColor.Yellow);
                    break;
            }
        }

        private static void SetVariable(Variable variable)
        {
            try
            {
                switch (variable.Type)
                {
                    case "string":
                        _proxy.Variables.Set(variable.Name, variable.Value.ToString());
                        break;

                    case "int32":
                        _proxy.Variables.Set(variable.Name, int.Parse(variable.Value.ToString()));
                        break;

                    case "int64":
                    case "decimal":
                        _proxy.Variables.Set(variable.Name, decimal.Parse(variable.Value.ToString()));
                        break;

                    case "date":
                        _proxy.Variables.Set(variable.Name,
                            DateTime.ParseExact(variable.Value.ToString(), "d-M-yyyy HH:mm:ss",
                                CultureInfo.InvariantCulture));
                        break;

                    case "boolean":
                        _proxy.Variables.Set(variable.Name, bool.Parse(variable.Value.ToString()));
                        break;
                }
                
                Log($"Set {variable.Name} to {variable.Value} ({variable.Type})", VoiceAttackColor.Pink);
            }
            catch (Exception ex)
            {
                Log($"Could not set {variable.Name} to '{variable.Value}': {ex.Message} ({ex.GetType().Name})");
            }
        }

        private static void Log(string message, VoiceAttackColor color = VoiceAttackColor.Purple)
        {
            _proxy.Log.Write($"[EliteAPI] {message}", color);
        }
    }
}