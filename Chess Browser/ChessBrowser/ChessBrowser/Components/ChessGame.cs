using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly Regex tagRegex = new Regex(@"\[(\w+) \"(.*?)\"]");
        private static readonly Regex moveSectionRegex = new Regex(@"\n\n(.*?)(?=\n\n|$)", RegexOptions.Singleline);

        public static List<ChessGame> ParsePgnFile(string filePath)
        {
            var games = new List<ChessGame>();
            var content = File.ReadAllText(filePath);
            var gameSections = content.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < gameSections.Length; i += 2) // Each game has metadata followed by moves
            {
                var game = new ChessGame();
                var metadata = gameSections[i];
                var moves = i + 1 < gameSections.Length ? gameSections[i + 1] : "";
                
                foreach (Match match in tagRegex.Matches(metadata))
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
                games.Add(game);
            }
            return games;
        }

        private static char ParseResult(string result)
        {
            return result switch
            {
                "1-0" => 'W',
                "0-1" => 'B',
                "1/2-1/2" => 'D',
                _ => '?'
            };
        }
    }
}
