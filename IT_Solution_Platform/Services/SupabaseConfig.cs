using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Supabase;

namespace IT_Solution_Platform.Services
{
    public class SupabaseConfig
    {

        private static Client _anonClient;
        private static Client _serviceRoleClient;
        private static readonly object _lock = new object();

        // Use ConfigurationManager instead of Environment variables for .NET Framework
        public static readonly string SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
             ?? ConfigurationManager.AppSettings["SUPABASE_URL"];

        public static readonly string SupabaseAnonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
            ?? ConfigurationManager.AppSettings["SUPABASE_ANON_KEY"];

        public static readonly string SupabaseServiceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? ConfigurationManager.AppSettings["SUPABASE_SERVICE_KEY"];

        public static readonly string SupabaseDbConnection = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION")
            ?? ConfigurationManager.AppSettings["SUPABASE_DB_CONNECTION"];

        // Singleton instance for anon client
        public static Client GetAnonClient()
        {
            if (_anonClient == null)
            {
                lock (_lock)
                {
                    if (_anonClient == null)
                    {
                        _anonClient = new Client(SupabaseUrl, SupabaseAnonKey, GetAnonClientOptions());
                    }
                }
            }
            return _anonClient;
        }

        // Singleton instance for service role client
        public static Client GetServiceClient()
        {
            if (_serviceRoleClient == null)
            {
                lock (_lock)
                {
                    if (_serviceRoleClient == null)
                    {
                        _serviceRoleClient = new Client(SupabaseUrl, SupabaseServiceKey, GetServiceRoleOptions());
                    }
                }
            }
            return _serviceRoleClient;
        }

        // Validation method
        public static bool IsConfigured()
        {
            return !string.IsNullOrEmpty(SupabaseUrl) &&
                   !string.IsNullOrEmpty(SupabaseAnonKey) &&
                   !string.IsNullOrEmpty(SupabaseServiceKey);
        }

        // Get default options for anon client
        public static SupabaseOptions GetAnonClientOptions()
        {
            return new SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = true,
                Headers = new Dictionary<string, string>
            {
                { "apikey", SupabaseAnonKey }
            }
            };
        }

        // Get options for service role client
        public static SupabaseOptions GetServiceRoleOptions()
        {
            return new SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = false,
                Headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {SupabaseServiceKey}" },
                { "apikey", SupabaseServiceKey }
            }
            };
        }


        // Initialization method to ensure clients are created
        public static async Task Initialize()
        {
            if (!IsConfigured())
            {
                throw new InvalidOperationException("Supabase configuration is missing or incomplete.");
            }

            // Initialize both clients
            await GetAnonClient().InitializeAsync();
            await GetServiceClient().InitializeAsync();
        }
    }
}