using System;
using System.Collections.Generic;
using System.Linq;
using Monopoly.Server.Models.State;
using Monopoly.Shared.Models.Configs.StaticData;
using Monopoly.Server.Models.Events;

namespace Monopoly.Server.GameLogic
{
    public static class GameEngineTests
    {
        public static void Run()
        {
            Console.WriteLine("\n=== RUNNING GAME ENGINE TESTS ===");

            try
            {
                TestColorMonopolyRentBonus();
                TestResortMonopolyVictory();
                TestLineMonopolyVictory();
                TestTripleMonopolyVictory();
                TestWorldTourChoice();
                TestStartNextTurn();
                TestEndReasonLastPlayerStanding();
                TestSendPlayerToIsland();

                Console.WriteLine("=== ALL TESTS PASSED SUCCESSFULLY! ===\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[TEST FAILURE] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Environment.Exit(1);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }

        private static GameState CreateMockGameState()
        {
            GameState state = new GameState
            {
                RoomId = "TEST",
                MapName = "Classic"
            };

            state.Players.Add(new GamePlayerState { PlayerIndex = 0, Username = "PlayerA", Money = 500000, IsConnected = true });
            state.Players.Add(new GamePlayerState { PlayerIndex = 1, Username = "PlayerB", Money = 500000, IsConnected = true });

            foreach (var kvp in BoardDatabase.Squares)
            {
                var square = kvp.Value;
                state.Properties[square.PositionIndex] = new GamePropertyState
                {
                    PositionIndex = square.PositionIndex,
                    Name = square.Name,
                    Type = square.Type,
                    ColorSet = square.ColorSet,
                    LineIndex = square.LineIndex,
                    BuyPrice = square.BuyPrice,
                    RentPrices = new List<long>(square.RentPrices ?? new List<long> { 0 }),
                    OwnerPlayerIndex = -1,
                    HouseCount = 0,
                    HasHotel = false,
                    Multiplier = 1,
                    PowerOutageTurn = 0
                };
            }

            return state;
        }

        private static void TestColorMonopolyRentBonus()
        {
            Console.WriteLine("Testing Color Monopoly Rent Bonus...");
            GameState state = CreateMockGameState();

            // Tokyo (1) and Osaka (3) belong to ColorSet = "Pink"
            var tokyo = state.Properties[1];
            var osaka = state.Properties[3];

            tokyo.OwnerPlayerIndex = 0;
            osaka.OwnerPlayerIndex = 0;

            // Tokyo base rent is 2000. Under Color Monopoly, multiplier is +1 (base Multiplier is 1, so total multiplier becomes 2).
            long rentWithMonopoly = GameEngine.GetCurrentRentUnsafe(state, tokyo);
            Assert(rentWithMonopoly == 4000, $"Expected rent 4000 with Color Monopoly, got {rentWithMonopoly}");

            // If PlayerB owns Osaka, PlayerA no longer has Color Monopoly
            osaka.OwnerPlayerIndex = 1;
            long rentWithoutMonopoly = GameEngine.GetCurrentRentUnsafe(state, tokyo);
            Assert(rentWithoutMonopoly == 2000, $"Expected rent 2000 without Color Monopoly, got {rentWithoutMonopoly}");

            Console.WriteLine("Color Monopoly Rent Bonus test passed.");
        }

        private static void TestResortMonopolyVictory()
        {
            Console.WriteLine("Testing Resort Monopoly Victory...");
            GameState state = CreateMockGameState();

            // Total resorts: 5. Resort monopoly requires owning >= 4.
            // Resorts: Hawaii (6), Nice (7), Dubai (14), Cyprus (18), Bali (28)
            state.Properties[6].OwnerPlayerIndex = 0;
            state.Properties[7].OwnerPlayerIndex = 0;
            state.Properties[14].OwnerPlayerIndex = 0;
            state.Properties[18].OwnerPlayerIndex = 0;

            List<string> messages = new List<string>();
            GameEngine.ResolveBankruptcyAndWinnerUnsafe(state, state.Players[0], messages);

            Assert(state.IsFinished, "Game should be finished on Resort Monopoly");
            Assert(state.WinnerUsername == "PlayerA", "PlayerA should be the winner");
            Assert(state.EndReason.Contains("Resort Monopoly"), $"Expected Resort Monopoly victory reason, got: {state.EndReason}");

            Console.WriteLine("Resort Monopoly Victory test passed.");
        }

        private static void TestLineMonopolyVictory()
        {
            Console.WriteLine("Testing Line Monopoly Victory...");
            GameState state = CreateMockGameState();

            // Line 1 cities: Tokyo (1), Osaka (3), Paris (5)
            state.Properties[1].OwnerPlayerIndex = 0;
            state.Properties[3].OwnerPlayerIndex = 0;
            state.Properties[5].OwnerPlayerIndex = 0;

            List<string> messages = new List<string>();
            GameEngine.ResolveBankruptcyAndWinnerUnsafe(state, state.Players[0], messages);

            Assert(state.IsFinished, "Game should be finished on Line Monopoly");
            Assert(state.WinnerUsername == "PlayerA", "PlayerA should be the winner");
            Assert(state.EndReason.Contains("Line Monopoly"), $"Expected Line Monopoly victory reason, got: {state.EndReason}");

            Console.WriteLine("Line Monopoly Victory test passed.");
        }

        private static void TestTripleMonopolyVictory()
        {
            Console.WriteLine("Testing Triple Monopoly Victory...");
            GameState state = CreateMockGameState();

            // Group 1 (Pink): Tokyo (1), Osaka (3)
            state.Properties[1].OwnerPlayerIndex = 0;
            state.Properties[3].OwnerPlayerIndex = 0;

            // Group 2 (Green): Sydney (13), London (15)
            state.Properties[13].OwnerPlayerIndex = 0;
            state.Properties[15].OwnerPlayerIndex = 0;

            // Group 3 (Cyan): Hà Nội (29), Sài Gòn (31)
            state.Properties[29].OwnerPlayerIndex = 0;
            state.Properties[31].OwnerPlayerIndex = 0;

            List<string> messages = new List<string>();
            GameEngine.ResolveBankruptcyAndWinnerUnsafe(state, state.Players[0], messages);

            Assert(state.IsFinished, "Game should be finished on Triple Monopoly");
            Assert(state.WinnerUsername == "PlayerA", "PlayerA should be the winner");
            Assert(state.EndReason.Contains("Triple Monopoly"), $"Expected Triple Monopoly victory reason, got: {state.EndReason}");

            Console.WriteLine("Triple Monopoly Victory test passed.");
        }

        private static void TestWorldTourChoice()
        {
            var state = CreateMockGameState();
            var player = state.Players[0];
            player.Position = 10;
            player.Money = 500000;

            List<string> msgs = new List<string>();
            List<CardDrawEvent> drawEvents = new List<CardDrawEvent>();
            string failMsg;

            // Target position 5 (passed Start, should get 200,000 bonus)
            bool success = GameEngine.TryApplyHeldCardEffectUnsafe(state, player, "WORLD_TOUR", 5, msgs, drawEvents, out failMsg);

            Assert(success, $"WORLD_TOUR should succeed, but failed with: {failMsg}");
            Assert(player.Position == 5, "Player should be moved to position 5");
            Assert(player.Money == 700000, "Player should receive pass Start bonus");

            Console.WriteLine("World Tour Choice test passed.");
        }

        private static void TestEndReasonLastPlayerStanding()
        {
            Console.WriteLine("Testing EndReason for last player standing...");
            GameState state = CreateMockGameState();

            // PlayerB hết tiền (âm) -> phá sản -> chỉ còn PlayerA trụ lại.
            state.Players[1].Money = -1;

            List<string> messages = new List<string>();
            GameEngine.ResolveBankruptcyAndWinnerUnsafe(state, state.Players[1], messages);

            Assert(state.IsFinished, "Game should finish when only one active player remains");
            Assert(state.WinnerUsername == "PlayerA", $"PlayerA should win, got {state.WinnerUsername}");
            Assert(!string.IsNullOrWhiteSpace(state.EndReason), "EndReason should not be empty for last-player-standing");

            Console.WriteLine($"EndReason last-player test passed (reason='{state.EndReason}').");
        }

        private static void TestSendPlayerToIsland()
        {
            Console.WriteLine("Testing SendPlayerToIsland (3-doubles jail target)...");
            GameState state = CreateMockGameState();
            GamePlayerState player = state.Players[0];
            player.Position = 10;

            GameEngine.SendPlayerToIslandUnsafe(player);

            Assert(player.Position == 24, $"Player should be at island position 24, got {player.Position}");
            Assert(player.IsOnIsland, "Player should be flagged on island");
            Assert(player.JailTurnsLeft >= 3, $"Player should have >=3 jail turns, got {player.JailTurnsLeft}");

            Console.WriteLine("SendPlayerToIsland test passed.");
        }

        private static void TestStartNextTurn()
        {
            var state = CreateMockGameState();
            state.Players[1].SkipTurnsLeft = 1;
            
            GameEngine.StartNextTurnUnsafe(state, out GamePlayerState? nextPlayer);
            
            Assert(nextPlayer!.PlayerIndex == 0, $"Expected Player 0, got {nextPlayer.PlayerIndex}");
            Assert(state.Players[1].SkipTurnsLeft == 0, "Player 1 skip turns should be decremented");
            Assert(state.CurrentTurnPlayerIndex == 0, "Current player index should be updated");

            Console.WriteLine("Start Next Turn test passed.");
        }
    }
}
