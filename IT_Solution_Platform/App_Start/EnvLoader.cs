using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace IT_Solution_Platform.App_Start
{
    public class EnvLoader
    {

        public static void LoadEnv(string filePath = "C:\\Users\\Abukhass\\source\\repos\\IT_Solution_Platform\\IT_Solution_Platform\\.env") 
        {
            if (!File.Exists(filePath))
                return;

            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines) 
            {
                if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');

                Environment.SetEnvironmentVariable(key, value);
            }
        
        }
    }
}