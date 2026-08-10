using RocketLeagueGameDataAPI;
using RocketLeagueGameDataAPI.Commands;
using RocketLeagueGameDataAPI.Events;
using RocketLeagueGameDataAPI.Models;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace RocketLeagueCompanion
{
    internal class Program
    {
        public static readonly (int, string)[] TeamColors = [(0, "Blue"), (1, "Orange")];

        private static bool _matchInfoPrinted = true;

        static private string Hyperlink(string link, string text) {
            return $"\x1b]8;;{link}\x1b\\{text}\x1b]8;;\x1b\\";
        }

        static private string OnlinePlatformToTrnPlatform(OnlinePlatform platform) => platform switch
        {
            OnlinePlatform.OnlinePlatform_Steam => "steam",
            OnlinePlatform.OnlinePlatform_PS4 => "psn",
            OnlinePlatform.OnlinePlatform_Dingo => "xbl",
            OnlinePlatform.OnlinePlatform_NNX => "switch",
            OnlinePlatform.OnlinePlatform_Epic => "epic",
            _ => throw new ArgumentException("Platform not supported!", nameof(platform)),
        };

        static private void PrintMatchInfo(Event_UpdateState state) {
            Console.WriteLine("\n+----------------------------------------+");
            Console.WriteLine($"| Match {state.MatchGuid} |");
            Console.WriteLine("+----------------------------------------+");
            foreach ((int teamNum, string color) in TeamColors)
            {
                Console.WriteLine($"| {color}");
                foreach (var player in state.Players)
                {
                    if (player.TeamNum != teamNum)
                    {
                        continue;
                    }
                    Console.Write("|   ");
                    PrintPlayerInfo(player.PrimaryId, player.Name);
                }
            }
            Console.WriteLine("+----------------------------------------+\n");
        }

        static private void PrintPlayerInfo(UniqueNetId primaryId, string playerName) {
            Console.Write($"- {playerName} - {primaryId} - ");
            if (primaryId.Platform == OnlinePlatform.OnlinePlatform_Unknown)
            {
                Console.Write("BOT");
            }
            else
            {
                var id = primaryId.Uid.ToString();
                if (primaryId.Platform == OnlinePlatform.OnlinePlatform_Epic)
                {
                    id = primaryId.EpicAccountId;
                }
                var rlstats = Hyperlink(
                    "https://rlstats.net/profile/" +
                    $"{UniqueNetId.OnlinePlatformToString(primaryId.Platform)}/{id}",
                    "RLStats"
                );
                var trnId = playerName;
                if (primaryId.Platform == OnlinePlatform.OnlinePlatform_Steam)
                {
                    trnId = primaryId.Uid.ToString();
                }
                var trn = Hyperlink(
                    "https://rocketleague.tracker.network/rocket-league/profile/" +
                    $"{OnlinePlatformToTrnPlatform(primaryId.Platform)}/{trnId}/overview",
                    "TRN"
                );
                Console.Write($"{rlstats} - {trn}");
            }
            Console.WriteLine("");
        }

        static async Task Main(string[] _)
        {
            using var rl = new RLGameDataAPIWS();

            Console.WriteLine("Trying to connect to game...");
            while (true)
            {
                try
                {
                    await rl.ConnectAsync();
                    break;
                }
                catch (WebSocketException)
                {
                    Thread.Sleep(1000);
                }
            }
            Console.WriteLine("Connected to the game!");

            Console.WriteLine("Reading...");
            while (rl.Connected)
            {
                try
                {
                    var events = await rl.ReceiveEventsAsync();
                    foreach (var e in events)
                    {
                        if (e is Event_MatchInitialized)
                        {
                            _matchInfoPrinted = false;
                        }
                        else if (e is Event_UpdateState state && !_matchInfoPrinted)
                        {
                            PrintMatchInfo(state);
                            _matchInfoPrinted = true;
                        }
                        else if (e is Event_PlayerJoined playerEvent)
                        {
                            PrintPlayerInfo(playerEvent.PrimaryId, playerEvent.PlayerName);
                        }
                    }
                }
                catch (WebSocketException)
                {
                    Console.WriteLine("Game connection was foribly closed by game!");
                    break;
                }
            }

            Console.WriteLine("Closing...");
            Thread.Sleep(1000);
        }
    }
}
