﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using EliteVA.Models;
using EliteVA.Processors;
using Newtonsoft.Json;
using Valsom.VoiceAttack;
using Valsom.VoiceAttack.Log;
using WebSocket4Net;
using ErrorEventArgs = SuperSocket.ClientEngine.ErrorEventArgs;

namespace EliteVA
{
    public class VoiceAttackPlugin
    {
        public static Guid VA_Id() => new Guid("189a4e44-caf1-459b-b62e-fabc60a12986");

        public static string VA_DisplayName() => "EliteVA";

        public static string VA_DisplayInfo() => "EliteVA by Somfic";

        private static WebSocket _socket;
        private static VoiceAttackProxy _proxy;
        
        private static bool _hasConnected = false;
        private static bool _hasCaughtUp = false;

        public static void VA_Init1(dynamic vaProxy)
        {
            Directory.CreateDirectory(Paths.VariablesDirectory.FullName);
            File.WriteAllText(Path.Combine(Paths.VariablesDirectory.FullName, "Event.txt"), string.Empty);
            
            _proxy = new VoiceAttackProxy(vaProxy);
            _socket = new WebSocket("ws://localhost:51555/ws", "EliteAPI-plugin");

            _socket.Opened += SocketOpened;
            _socket.Error += SocketError;
            _socket.Closed += SocketClosed;
            _socket.MessageReceived += async (sender, e) => await SocketMessage(e);

            _hasConnected = false; 
            Log("Connecting to EliteAPI Hub ...", VoiceAttackColor.Gray);
            _socket.Open();
        }

        private static async Task SocketMessage(MessageReceivedEventArgs e)
        {
            await ReceivedMessage(JsonConvert.DeserializeObject<WebSocketMessage>(e.Message));
        }

        private static void SocketClosed(object sender, EventArgs e)
        {
            if (_hasConnected)
            {
                Log("The connection to EliteAPI Hub was closed", VoiceAttackColor.Red);
                Log($"Trying to reconnect to EliteAPI Hub ... ", VoiceAttackColor.Gray);
            }

            _hasConnected = false;
            _socket.Open();
        }

        private static void SocketError(object sender, ErrorEventArgs e)
        {
            if (_hasConnected)
            {
                Log($"Could not connect to EliteAPI Hub ({e.Exception.GetType().Name}: {e.Exception})",
                    VoiceAttackColor.Purple);
            }
        }

        private static void SocketOpened(object sender, EventArgs e)
        {
            _hasConnected = true;
            Log("Authenticating with EliteAPI Hub", VoiceAttackColor.Gray);
            SendMessage(new WebSocketMessage("auth", "EliteVA"));
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

        private static async Task ReceivedMessage(WebSocketMessage message)
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
                        SetVariables(eventValue.Variables, message.Type);
                        if (_hasCaughtUp)
                        {
                            //Log($"Invoking ((EliteAPI.{eventValue.Name}))", VoiceAttackColor.Pink);
                            var commandName = $"((EliteAPI.{eventValue.Name}))";
                            if(await _proxy.Commands.Exists(commandName))
                            {
                                await _proxy.Commands.Invoke(commandName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"{ex.GetType().Name}: {ex.Message}");
                    }

                    break;

                case "Bindings":
                    // To convert an XML node contained in string xml into a JSON string   
                    List<Variable> bindings = BindingsProcessor.SetBindings(value);
                    SetVariables(bindings, "Bindings");
                    break;
            }
        }

        private static void SetVariables(IList<Variable> variables, string category, bool log = false)
        {
            try
            {
                Directory.CreateDirectory(Paths.VariablesDirectory.FullName);
                if (category != "Event")
                {
                    File.WriteAllLines(Path.Combine(Paths.VariablesDirectory.FullName, category + ".txt"),
                        variables.Select(x => $"{{{x.GetType()}:EliteAPI.{x.Name}}}: {x.Value}"));
                }
                else
                {
                    File.AppendAllLines(Path.Combine(Paths.VariablesDirectory.FullName, category + ".txt"),
                        variables.Select(x => $"{{{x.GetType()}:EliteAPI.{x.Name}}}: {x.Value}"));
                }
               
                variables.ToList().ForEach(x => SetVariable(x, log));
            }
            catch (Exception ex)
            {
                Log(ex, $"Could not set variables for {category}");
            }
        }
        
        private static void SetVariable(Variable variable, bool log)
        {
            try
            {
                variable.Name = "EliteAPI." + variable.Name;
                
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
                        _proxy.Variables.Set(variable.Name, DateTime.Parse(variable.Value.ToString()));
                        break;

                    case "boolean":
                        _proxy.Variables.Set(variable.Name, bool.Parse(variable.Value.ToString()));
                        break;
                }

                if (log)
                {
                    Log($"Set {variable.Name} to {variable.Value} ({variable.Type})", VoiceAttackColor.Pink);
                }
            }
            catch (Exception ex)
            {
                Log($"Could not set {variable.Name} to '{variable.Value}'. {GetPrettyExceptionName(ex)}: {ex.Message}");
            }
        }

        public static void Log(string message, VoiceAttackColor color = VoiceAttackColor.Purple)
        {
            _proxy.Log.Write($"[EliteAPI] {message}", color);
        }
        
        public static void Log(Exception ex, string message, VoiceAttackColor color = VoiceAttackColor.Red)
        {
            _proxy.Log.Write($"[EliteAPI] ({GetPrettyExceptionName(ex)}: {ex.Message}) {message}", color);
        }
        
        private static string GetPrettyExceptionName(Exception ex)
        {
            var output = Regex.Replace(ex.GetType().Name, @"\p{Lu}", m => " " + m.Value.ToLowerInvariant());
            output = ex.GetType().Name == "Exception" ? "Exception" : output.Replace("exception", "");
            output = output.Trim();
            output = char.ToUpperInvariant(output[0]) + output.Substring(1);
            return output;
        }
    }
}
