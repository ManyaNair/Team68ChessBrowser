using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace ChessBrowser.Components.Pages
{
    public partial class ChessBrowser
    {
        private string Username = "";
        private string Password = "";
        private string Database = "";
        private int Progress = 0;

        private async Task InsertGameData(string[] PGNFileLines)
        {
            string connection = GetConnectionString();
            var games = PgnParser.ParsePgnFile(PGNFileLines);

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                try
                {
                    conn.Open();

                    for (int i = 0; i < games.Count; i++)
                    {
                        var game = games[i];

                        // Update Elo ratings to store the highest value seen
                        UpdateHighestElo(conn, game.White, game.WhiteElo);
                        UpdateHighestElo(conn, game.Black, game.BlackElo);

                        string insertCommand = @"
                            INSERT INTO ChessGames (Event, Site, Round, White, Black, WhiteElo, BlackElo, Result, EventDate, Moves)
                            VALUES (@Event, @Site, @Round, @White, @Black, 
                                    (SELECT MAX(Elo) FROM EloRatings WHERE Player = @White),
                                    (SELECT MAX(Elo) FROM EloRatings WHERE Player = @Black),
                                    @Result, @EventDate, @Moves)";

                        using (MySqlCommand cmd = new MySqlCommand(insertCommand, conn))
                        {
                            cmd.Parameters.AddWithValue("@Event", game.Event);
                            cmd.Parameters.AddWithValue("@Site", game.Site);
                            cmd.Parameters.AddWithValue("@Round", game.Round);
                            cmd.Parameters.AddWithValue("@White", game.White);
                            cmd.Parameters.AddWithValue("@Black", game.Black);
                            cmd.Parameters.AddWithValue("@Result", game.Result);
                            cmd.Parameters.AddWithValue("@EventDate", game.EventDate);
                            cmd.Parameters.AddWithValue("@Moves", game.Moves);

                            cmd.ExecuteNonQuery();
                        }

                        // Update progress bar
                        Progress = (i + 1) * 100 / games.Count;
                        await InvokeAsync(StateHasChanged);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error inserting data: {e.Message}");
                }
            }
        }

        private void UpdateHighestElo(MySqlConnection conn, string player, int elo)
        {
            if (string.IsNullOrEmpty(player) || elo <= 0) return;

            string checkQuery = "SELECT Elo FROM EloRatings WHERE Player = @Player";
            using (MySqlCommand cmd = new MySqlCommand(checkQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Player", player);
                object result = cmd.ExecuteScalar();

                if (result != null)
                {
                    int existingElo = Convert.ToInt32(result);
                    if (elo > existingElo)
                    {
                        string updateQuery = "UPDATE EloRatings SET Elo = @Elo WHERE Player = @Player";
                        using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@Elo", elo);
                            updateCmd.Parameters.AddWithValue("@Player", player);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                }
                else
                {
                    string insertQuery = "INSERT INTO EloRatings (Player, Elo) VALUES (@Player, @Elo)";
                    using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Player", player);
                        insertCmd.Parameters.AddWithValue("@Elo", elo);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private string PerformQuery(string white, string black, string opening,
            string winner, bool useDate, DateTime start, DateTime end, bool showMoves)
        {
            string connection = GetConnectionString();
            string parsedResult = "";
            int numRows = 0;

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                try
                {
                    conn.Open();

                    string query = "SELECT Event, Site, Round, White, Black, WhiteElo, BlackElo, Result, EventDate";
                    if (showMoves)
                    {
                        query += ", Moves";
                    }
                    query += " FROM ChessGames WHERE 1=1";

                    if (!string.IsNullOrEmpty(white)) query += " AND White = @White";
                    if (!string.IsNullOrEmpty(black)) query += " AND Black = @Black";
                    if (!string.IsNullOrEmpty(opening)) query += " AND Moves LIKE @Opening";
                    if (!string.IsNullOrEmpty(winner)) query += " AND Result = @Winner";
                    if (useDate) query += " AND EventDate BETWEEN @Start AND @End";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(white)) cmd.Parameters.AddWithValue("@White", white);
                        if (!string.IsNullOrEmpty(black)) cmd.Parameters.AddWithValue("@Black", black);
                        if (!string.IsNullOrEmpty(opening)) cmd.Parameters.AddWithValue("@Opening", opening + "%");
                        if (!string.IsNullOrEmpty(winner)) cmd.Parameters.AddWithValue("@Winner", winner);
                        if (useDate)
                        {
                            cmd.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd"));
                        }

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                numRows++;
                                parsedResult += $"Event: {reader["Event"]}\n" +
                                                $"Site: {reader["Site"]}\n" +
                                                $"Date: {reader["EventDate"]}\n" +
                                                $"White: {reader["White"]} ({reader["WhiteElo"]})\n" +
                                                $"Black: {reader["Black"]} ({reader["BlackElo"]})\n" +
                                                $"Result: {reader["Result"]}\n";

                                if (showMoves)
                                {
                                    parsedResult += $"Moves: {reader["Moves"]}\n";
                                }

                                parsedResult += "\n";
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error performing query: {e.Message}");
                }
            }

            return $"{numRows} results\n\n{parsedResult}";
        }

        private string GetConnectionString()
        {
            return $"server=atr.eng.utah.edu;database={Database};uid={Username};password={Password}";
        }

        private async void HandleFileChooser(EventArgs args)
        {
            try
            {
                string fileContent = string.Empty;

                InputFileChangeEventArgs eventArgs = args as InputFileChangeEventArgs 
                    ?? throw new Exception("Unable to get file name");

                if (eventArgs.FileCount == 1)
                {
                    var file = eventArgs.File;
                    if (file == null) return;

                    using var stream = file.OpenReadStream(1000000);
                    using var reader = new StreamReader(stream);

                    fileContent = await reader.ReadToEndAsync();
                    string[] fileLines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    await InsertGameData(fileLines);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading file: {e.Message}");
            }
        }
    }
}
