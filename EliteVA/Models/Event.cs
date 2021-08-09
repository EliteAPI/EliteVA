using System.Collections.Generic;
using Newtonsoft.Json;

namespace EliteVA.Models
{
    public class Event
    {
        [JsonConstructor]
        public Event()
        {
            
        }
        
        public Event(string name, List<Variable> variables)
        {
            Name = name;
            Variables = variables;
        }

        public string Name { get; set; }
        
        public List<Variable> Variables { get; set; }
    }
}