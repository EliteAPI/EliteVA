using System;
using Newtonsoft.Json;

namespace EliteVA.Models
{
    public class Variable
    {
        [JsonConstructor]
        public Variable()
        {
            
        }
        
        public Variable(string name, object value, string type)
        {
            Name = name;
            Value = value;
            Type = type;
        }
        
        public Variable(string name, string value)
        {
            Name = name;
            Value = value;
            Type = "TXT";
        }

        public Variable(string name, int value)
        {
            Name = name;
            Value = value;
            Type = "INT";
        }

        public Variable(string name, long value)
        {
            Name = name;
            Value = value;
            Type = "DEC";
        }

        public Variable(string name, decimal value)
        {
            Name = name;
            Value = value;
            Type = "DEC";
        }

        public Variable(string name, DateTime value)
        {
            Name = name;
            Value = value;
            Type = "DATE";
        }

        public Variable(string name, bool value)
        {
            Name = name;
            Value = value;
            Type = "BOOL";
        }

        public string Name { get; set; }
        public object Value { get; set; }
        public string Type { get; set; }
    }
}