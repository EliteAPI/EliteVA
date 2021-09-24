using System;
using System.IO;
using System.Reflection;

namespace EliteVA
{
    public static class Paths
    {
        public static DirectoryInfo PluginDirectory => new (Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory);
        
        public static DirectoryInfo MappingsDirectory =>  new (Path.Combine(PluginDirectory.FullName, "Mappings"));
        
        public static DirectoryInfo VariablesDirectory =>  new (Path.Combine(PluginDirectory.FullName, "Variables"));
    }
}