using MessengerServer.Data;
using MessengerServer.Models;
using Microsoft.Data.Sqlite;

namespace MessengerServer.Repositories
{
    public static class UserRepository
    {
        public static bool TryCreate(string username, string displayName, string passwordHash, out int newId)
        {
            newId = 0;
            try
            {
                using var con = Database.OpenConnection();
                var cmd = con.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Users (Username, DisplayName, PasswordHash, CreatedAt)
                    VALUES ($u, $d, $h, $t);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$u", username.Trim().ToLower());
                cmd.Parameters.AddWithValue("$d", displayName.Trim());
                cmd.Parameters.AddWithValue("$h", passwordHash);
                cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
                newId = Convert.ToInt32(cmd.ExecuteScalar());
                return true;
            }
            catch (SqliteException)
            {
                return false; // UNIQUE constraint = username taken
            }
        }

        public static User? FindByUsername(string username)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, DisplayName, PasswordHash FROM Users WHERE Username=$u";
            cmd.Parameters.AddWithValue("$u", username.Trim().ToLower());
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new User
            {
                Id = r.GetInt32(0),
                Username = r.GetString(1),
                DisplayName = r.GetString(2),
                PasswordHash = r.GetString(3)
            };
        }

        public static string? GetDisplayName(int userId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT DisplayName FROM Users WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", userId);
            return cmd.ExecuteScalar()?.ToString();
        }

        public static void SetOnline(int userId, bool online)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE Users SET IsOnline=$v WHERE Id=$id";
            cmd.Parameters.AddWithValue("$v", online ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", userId);
            cmd.ExecuteNonQuery();
        }

        public static List<object> Search(string query, int excludeId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Username, DisplayName, IsOnline FROM Users
                WHERE (Username LIKE $q OR DisplayName LIKE $q) AND Id != $id
                LIMIT 20";
            cmd.Parameters.AddWithValue("$q", $"%{query}%");
            cmd.Parameters.AddWithValue("$id", excludeId);
            using var r = cmd.ExecuteReader();
            var list = new List<object>();
            while (r.Read())
                list.Add(new
                {
                    Id = r.GetInt32(0),
                    Username = r.GetString(1),
                    DisplayName = r.GetString(2),
                    IsOnline = r.GetInt32(3) == 1
                });
            return list;
        }
    }
}
