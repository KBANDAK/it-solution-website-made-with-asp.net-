using System;
using Npgsql;
using Newtonsoft.Json;

namespace IT_Solution_Platform.Services
{
    /// <summary>
    /// Service for logging audit events to the audit_logs table.
    /// </summary>
    public class AuditLogService
    {

        private readonly SupabaseDatabase _database;

        public AuditLogService(SupabaseDatabase database)
        {
            _database = database;
        }
        /// <summary>
        /// Logs an audit event to the audit_logs table.
        /// </summary>
        /// <param name="userId">The Supabase user ID (nullable, corresponds to supabase_uid).</param>
        /// <param name="action">The action being logged (e.g., "Registration Attempt").</param>
        /// <param name="entityType">The type of entity affected (e.g., "User").</param>
        /// <param name="entityId">The ID of the entity affected (nullable).</param>
        /// <param name="details">Additional details about the event (serialized to JSON).</param>
        /// <param name="ipAddress">The IP address of the client.</param>
        /// <param name="userAgent">The user agent of the client.</param>
        /// <returns>True if the log was successfully inserted, false otherwise.</returns>
        public bool LogAudit(int userId, string action, string entityType, int? entityId, object details, string ipAddress, string userAgent)
        {
            try
            {
                string query = @"INSERT INTO audit_logs (user_id, action, entity_type, entity_id, details, ip_address, user_agent, created_at)
                                VALUES (@user_id, @action, @entity_type, @entity_id, @details, @ip_address, @user_agent, CURRENT_TIMESTAMP)";

                var parameters = new
                {
                    user_id = userId,
                    action,
                    entity_type = entityType,
                    entity_id = (object)entityId ?? DBNull.Value,
                    details = JsonConvert.SerializeObject(details),
                    ip_address = ipAddress,
                    user_agent = userAgent
                };

                return _database.ExecuteNonQuery(query, parameters) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LogAudit failed: {ex.Message}");
                return false;
            }
        }
    }
}