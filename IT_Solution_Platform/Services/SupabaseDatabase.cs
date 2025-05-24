using Npgsql;
using System;
using System.Linq; // Add this for LINQ extension methods like FirstOrDefault
using Newtonsoft.Json;
using System.Collections.Generic; // For working with collections

namespace IT_Solution_Platform.Services
{
    public class SupabaseDatabase
    {
        public T? ExecuteQuerySingle<T>(string query, object parameters = null) where T : struct
        {
            try
            {
                using (var connection = new NpgsqlConnection(SupabaseConfig.SupabaseDbConnection))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            foreach (var prop in parameters.GetType().GetProperties())
                            {
                                command.Parameters.AddWithValue(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
                            }
                        }
                        var result = command.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                        {
                            return null; // Return null for nullable value types
                        }
                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ExecuteQuerySingle failed: {ex.Message}");
                throw;
            }
        }

        public List<T> ExecuteQuery<T>(string query, object parameters = null) where T : new()
        {
            try
            {
                var results = new List<T>();
                using (var connection = new NpgsqlConnection(SupabaseConfig.SupabaseDbConnection))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            foreach (var prop in parameters.GetType().GetProperties())
                            {
                                command.Parameters.AddWithValue(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
                            }
                        }

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                T item = new T();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var property = typeof(T).GetProperty(reader.GetName(i),
                                        System.Reflection.BindingFlags.IgnoreCase |
                                        System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.Instance);

                                    if (property != null && reader[i] != DBNull.Value)
                                    {
                                        property.SetValue(item, Convert.ChangeType(reader[i], property.PropertyType));
                                    }
                                }
                                results.Add(item);
                            }
                        }
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ExecuteQuery failed: {ex.Message}");
                throw;
            }
        }

        public int ExecuteNonQuery(string query, object parameters)
        {
            try
            {
                using (var connection = new NpgsqlConnection(SupabaseConfig.SupabaseDbConnection))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            foreach (var prop in parameters.GetType().GetProperties())
                            {
                                command.Parameters.AddWithValue(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
                            }
                        }
                        return command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ExecuteNonQuery failed: {ex.Message}");
                throw;
            }
        }
    }
}