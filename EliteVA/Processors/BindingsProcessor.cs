using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EliteVA.Models;
using Valsom.VoiceAttack.Log;

namespace EliteVA.Processors
{
    public static class BindingsProcessor
    {
        public static IList<Variable> SetBindings(string rawXml)
        {
            try
            {
                XElement xml = XElement.Parse(rawXml);

                var layout = GetLayout(xml);
                var mapping = GetMapping(layout);
                var keys = GetVariables(xml, mapping).ToList();

                return keys;
            }
            catch (Exception ex)
            {
                VoiceAttackPlugin.Log(ex, "Could not set keybindings");
                return new List<Variable>();
            }
        }
        
        private static string GetLayout(XElement xml)
        {
            try
            {
                var layoutElement = xml.Element("KeyboardLayout");
                return layoutElement != null ? layoutElement.Value : "en-US";
            }
            catch (Exception ex)
            {
                VoiceAttackPlugin.Log(ex, "Could not get layout from keybindings");
                throw;
            }
        }

        private static IDictionary<string, string> GetMapping(string layout)
        {
            try
            {
                string mappingFile = Path.Combine(Paths.MappingsDirectory.FullName, $"{layout}.yml");

                if (!File.Exists(mappingFile))
                {
                    if (layout != "en-GB")
                    {
                        VoiceAttackPlugin.Log($"Could not find {layout}.yml, defaulting to en-GB.yml for mapping", VoiceAttackColor.Gray);
                        return GetMapping("en-GB");
                    }

                    VoiceAttackPlugin.Log("Could not set keybindings, no mappings files were found", VoiceAttackColor.Red);
                    return new Dictionary<string, string>();
                }

                VoiceAttackPlugin.Log($"Using mappings in '{mappingFile}'", VoiceAttackColor.Gray);

                var entries = File.ReadAllLines(mappingFile)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains(":"));
                return entries.ToDictionary(entry => entry.Split(':')[0].Trim(), entry => entry.Split(':')[1].Trim());
            }
            catch (Exception ex)
            {
                VoiceAttackPlugin.Log(ex, $"Could not get mappings from {layout}.yml");
                throw;
            }
        }

        private static IEnumerable<Variable> GetVariables(XElement xml, IDictionary<string, string> mapping)
        { 
            IList<Variable> variables = new List<Variable>();

            foreach (var bindingNode in xml.Elements().Where(i => i.Elements().Any()))
            {
                try
                {
                    var name = bindingNode.Name.LocalName;

                    var primary = bindingNode.Element("Primary");
                    var secondary = bindingNode.Element("Secondary");

                    if (primary == null)
                    {
                        //VoiceAttackPlugin.Log($"Skipping {name}, no bindings set");
                        continue;
                    }

                    XElement active = null;

                    if (IsApplicableBinding(primary))
                    {
                        active = primary;
                    }
                    else if (IsApplicableBinding(secondary))
                    {
                        active = secondary;
                    }
                    else
                    {
                        //VoiceAttackPlugin.Log($"Skipping {name}, not applicable");
                        continue;
                    }

                    if (active == null) continue;

                    var modifiers = active.Elements("Modifier").ToList();

                    if (modifiers.Any(x => !IsApplicableBinding(x)))
                    {
                        //VoiceAttackPlugin.Log($"Skipping {name}, modifier not applicable");
                    }

                    string value = GetKeyBinding(active.Attribute("Key").Value, modifiers.Select(x => x.Attribute("Key").Value), mapping);

                    variables.Add(new Variable(name, value));
                }
                catch (Exception ex)
                {
                    VoiceAttackPlugin.Log(ex, $"Could not process {bindingNode.Name} keybinding");
                }
            }

            return variables;
        }

        private static string GetKeyBinding(string key, IEnumerable<string> mods, IDictionary<string, string> mapping)
        {
            return string.Join("", mods.Select(mod => GetKey(mod, mapping))) + GetKey(key, mapping);
        }

        private static string GetKey(string key, IDictionary<string, string> mapping)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            key = key.Replace("Key_", "");

            if (!mapping.ContainsKey(key))
            {
                VoiceAttackPlugin.Log($"The '{key}' key is not assigned in the mappings file", VoiceAttackColor.Yellow);
                return "";
            }

            return $"[{mapping[key]}]";
        }

        private static bool IsApplicableBinding(XElement xml)
        {
            var deviceNode = xml.Attribute("Device");
            var keyNode = xml.Attribute("Key");

            return deviceNode != null && deviceNode.Value == "Keyboard" && keyNode != null &&
                   keyNode.Value.StartsWith("Key_");
        }
    }
}