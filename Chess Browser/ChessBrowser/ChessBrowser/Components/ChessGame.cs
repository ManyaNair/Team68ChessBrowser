using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChessBrowser
{
    public class ChessGame
    {
        public string Event { get; set; }
        public string Site { get; set; }
        public string Round { get; set; }
        public string White { get; set; }
        public string Black { get; set; }
        public int WhiteElo { get; set; }
        public int BlackElo { get; set; }
        public char Result { get; set; }
        public string EventDate { get; set; }
        public string Moves { get; set; }
    }

    public static class PgnParser
    {
        private static readonly Regex tagRegex = new Regex(@"\[(\w+) ""(.*?)""\]", RegexOptions.Compiled);
        
        public static List<ChessGame> ParsePgnFile(string[] fileLines)
        {
            var games = new List<ChessGame>();
            var currentGameData = new List<string>();

            foreach (var line in fileLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentGameData.Count > 0)
                    {
                        games.Add(ParseSingleGame(currentGameData));
                        currentGameData.Clear();
                    }
                }
                else
                {
                    currentGameData.Add(line);
                }
            }

            if (currentGameData.Count > 0)
            {
                games.Add(ParseSingleGame(currentGameData));
            }

            return games;
        }

        private static ChessGame ParseSingleGame(List<string> gameData)
        {
            var game = new ChessGame();
            var metadata = new List<string>();
            var moves = "";
            bool movesSection = false;

            foreach (var line in gameData)
            {
                if (line.StartsWith("["))
                {
                    metadata.Add(line);
                }
                else
                {
                    movesSection = true;
                    moves += " " + line.Trim();
                }
            }

            foreach (Match match in tagRegex.Matches(string.Join("\n", metadata)))
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                switch (key)
                {
                    case "Event": game.Event = value; break;
                    case "Site": game.Site = value; break;
                    case "Round": game.Round = value; break;
                    case "White": game.White = value; break;
                    case "Black": game.Black = value; break;
                    case "WhiteElo": game.WhiteElo = int.TryParse(value, out int wElo) ? wElo : 0; break;
                    case "BlackElo": game.BlackElo = int.TryParse(value, out int bElo) ? bElo : 0; break;
                    case "Result": game.Result = ParseResult(value); break;
                    case "EventDate": game.EventDate = value; break;
                }
            }

            game.Moves = moves.Trim();
            return game;
        }

        private static char ParseResult(string result)
        {
            switch (result)
            {
                case "1-0": return 'W';
                case "0-1": return 'B';
                case "1/2-1/2": return 'D';
                default: return '?';
            }
        }
    }
}
