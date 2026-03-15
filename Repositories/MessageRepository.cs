using MessengerServer.Data;
using MessengerServer.Models;

namespace MessengerServer.Repositories
{
    public static class MessageRepository
    {
        private const int MaxContentLength = 4000;

        public static (bool ok, string error) Send(int senderId, int receiverId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return (false, "Сообщение не может быть пустым");
            if (content.Length > MaxContentLength)
                return (false, $"Сообщение слишком длинное (максимум {MaxContentLength} символов)");

            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Messages (SenderId, ReceiverId, Content, SentAt)
                VALUES ($s, $r, $c, $t)";
            cmd.Parameters.AddWithValue("$s", senderId);
            cmd.Parameters.AddWithValue("$r", receiverId);
            cmd.Parameters.AddWithValue("$c", content.Trim());
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
            return (true, "");
        }

        public static List<Message> GetConversation(int userId, int friendId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT m.Id, m.SenderId, m.ReceiverId, m.Content, m.SentAt, m.IsRead, u.DisplayName
                FROM Messages m
                JOIN Users u ON u.Id = m.SenderId
                WHERE (m.SenderId=$u AND m.ReceiverId=$f)
                   OR (m.SenderId=$f AND m.ReceiverId=$u)
                ORDER BY m.SentAt ASC";
            cmd.Parameters.AddWithValue("$u", userId);
            cmd.Parameters.AddWithValue("$f", friendId);
            using var r = cmd.ExecuteReader();
            var list = new List<Message>();
            while (r.Read())
                list.Add(new Message
                {
                    Id = r.GetInt32(0),
                    SenderId = r.GetInt32(1),
                    ReceiverId = r.GetInt32(2),
                    Content = r.GetString(3),
                    SentAt = DateTime.Parse(r.GetString(4)),
                    IsRead = r.GetInt32(5) == 1,
                    SenderName = r.GetString(6)
                });

            // Mark as read in the same transaction
            var upd = con.CreateCommand();
            upd.CommandText = @"
                UPDATE Messages SET IsRead=1
                WHERE SenderId=$f AND ReceiverId=$u AND IsRead=0";
            upd.Parameters.AddWithValue("$f", friendId);
            upd.Parameters.AddWithValue("$u", userId);
            upd.ExecuteNonQuery();

            return list;
        }

        public static int GetUnreadCount(int userId, int friendId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Messages
                WHERE SenderId=$f AND ReceiverId=$u AND IsRead=0";
            cmd.Parameters.AddWithValue("$f", friendId);
            cmd.Parameters.AddWithValue("$u", userId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
