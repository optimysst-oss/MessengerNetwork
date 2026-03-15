using MessengerServer.Data;

namespace MessengerServer.Repositories
{
    public static class FriendRepository
    {
        public static List<object> GetFriends(int userId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT u.Id, u.Username, u.DisplayName, u.IsOnline
                FROM Friends f
                JOIN Users u ON u.Id = CASE WHEN f.UserId=$id THEN f.FriendId ELSE f.UserId END
                WHERE f.UserId=$id OR f.FriendId=$id
                ORDER BY u.DisplayName";
            cmd.Parameters.AddWithValue("$id", userId);
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

        public static bool AreFriends(int a, int b)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Friends
                WHERE (UserId=$a AND FriendId=$b) OR (UserId=$b AND FriendId=$a)";
            cmd.Parameters.AddWithValue("$a", a);
            cmd.Parameters.AddWithValue("$b", b);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public static (bool ok, string error) SendRequest(int senderId, int receiverId)
        {
            if (senderId == receiverId)
                return (false, "Нельзя добавить себя в друзья");

            using var con = Database.OpenConnection();

            var dupCheck = con.CreateCommand();
            dupCheck.CommandText = @"
                SELECT COUNT(*) FROM FriendRequests
                WHERE ((SenderId=$s AND ReceiverId=$r) OR (SenderId=$r AND ReceiverId=$s))
                  AND Status='pending'";
            dupCheck.Parameters.AddWithValue("$s", senderId);
            dupCheck.Parameters.AddWithValue("$r", receiverId);
            if (Convert.ToInt32(dupCheck.ExecuteScalar()) > 0)
                return (false, "Заявка уже отправлена");

            var friendCheck = con.CreateCommand();
            friendCheck.CommandText = @"
                SELECT COUNT(*) FROM Friends
                WHERE (UserId=$s AND FriendId=$r) OR (UserId=$r AND FriendId=$s)";
            friendCheck.Parameters.AddWithValue("$s", senderId);
            friendCheck.Parameters.AddWithValue("$r", receiverId);
            if (Convert.ToInt32(friendCheck.ExecuteScalar()) > 0)
                return (false, "Вы уже друзья");

            var ins = con.CreateCommand();
            ins.CommandText = @"
                INSERT INTO FriendRequests (SenderId, ReceiverId, CreatedAt)
                VALUES ($s, $r, $t)";
            ins.Parameters.AddWithValue("$s", senderId);
            ins.Parameters.AddWithValue("$r", receiverId);
            ins.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            ins.ExecuteNonQuery();
            return (true, "");
        }

        public static List<object> GetPendingRequests(int receiverId)
        {
            using var con = Database.OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT fr.Id, fr.SenderId, fr.ReceiverId, fr.Status, fr.CreatedAt, u.DisplayName
                FROM FriendRequests fr
                JOIN Users u ON u.Id = fr.SenderId
                WHERE fr.ReceiverId=$id AND fr.Status='pending'";
            cmd.Parameters.AddWithValue("$id", receiverId);
            using var r = cmd.ExecuteReader();
            var list = new List<object>();
            while (r.Read())
                list.Add(new
                {
                    Id = r.GetInt32(0),
                    SenderId = r.GetInt32(1),
                    ReceiverId = r.GetInt32(2),
                    Status = r.GetString(3),
                    CreatedAt = DateTime.Parse(r.GetString(4)),
                    SenderName = r.GetString(5)
                });
            return list;
        }

        /// <summary>Returns senderId on success so caller can notify via SignalR.</summary>
        public static (bool ok, string error, int senderId) Respond(int requestId, int respondingUserId, bool accept)
        {
            using var con = Database.OpenConnection();

            var get = con.CreateCommand();
            get.CommandText = "SELECT SenderId, ReceiverId FROM FriendRequests WHERE Id=$id";
            get.Parameters.AddWithValue("$id", requestId);
            using var r = get.ExecuteReader();
            if (!r.Read()) return (false, "Заявка не найдена", 0);

            int senderId = r.GetInt32(0);
            int receiverId = r.GetInt32(1);
            r.Close();

            // Guard: only the intended receiver can respond
            if (receiverId != respondingUserId)
                return (false, "Нет доступа", 0);

            var upd = con.CreateCommand();
            upd.CommandText = "UPDATE FriendRequests SET Status=$s WHERE Id=$id";
            upd.Parameters.AddWithValue("$s", accept ? "accepted" : "declined");
            upd.Parameters.AddWithValue("$id", requestId);
            upd.ExecuteNonQuery();

            if (accept)
            {
                var ins = con.CreateCommand();
                ins.CommandText = @"
                    INSERT OR IGNORE INTO Friends (UserId, FriendId, CreatedAt)
                    VALUES ($u, $f, $t)";
                ins.Parameters.AddWithValue("$u", senderId);
                ins.Parameters.AddWithValue("$f", receiverId);
                ins.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
                ins.ExecuteNonQuery();
            }

            return (true, "", senderId);
        }
    }
}
