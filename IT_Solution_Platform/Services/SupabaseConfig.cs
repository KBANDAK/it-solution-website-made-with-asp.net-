using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace IT_Solution_Platform.Services
{
    public class SupabaseConfig
    {
        
        public static readonly string SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        public static readonly string SupabaseAnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
        public static readonly string SupabaseServiceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
        public static readonly string SupabaseDbConnection = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION");
    }
}