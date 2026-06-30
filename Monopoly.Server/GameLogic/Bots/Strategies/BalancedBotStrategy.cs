using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using System.Linq;
using System.Collections.Generic;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public class BalancedBotStrategy : BotStrategyBase
    {
        public override bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            bool blockOpponent = BlockOpponentColorSet(gameState, bot, property);
            
            long safeBuffer = 100000;
            // Blocks opponent if it has at least 50k buffer, buys for itself if 100k buffer
            return bot.Money - property.BuyPrice > safeBuffer || completesColorSet || (blockOpponent && bot.Money - property.BuyPrice > 50000);
        }

        public override bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property)
        {
            long safeBuffer = 200000;
            long buildCost = GameEngine.GetBuildCostUnsafe(property);
            return buildCost > 0 && bot.Money - buildCost > safeBuffer;
        }

        public override int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType)
        {
            // Balanced: Target player with most properties (controlling the board)
            var opponents = gameState.Players.Where(p => !p.IsBankrupt && p.PlayerIndex != bot.PlayerIndex).ToList();
            if (opponents.Count == 0) return -1;
            
            var mostPropsPlayer = opponents.OrderByDescending(p => gameState.Properties.Values.Count(prop => prop.OwnerPlayerIndex == p.PlayerIndex)).FirstOrDefault();
            
            if (mostPropsPlayer != null)
            {
                var theirProps = gameState.Properties.Values
                    .Where(p => p.OwnerPlayerIndex == mostPropsPlayer.PlayerIndex && p.Type == "City")
                    .OrderByDescending(p => GameEngine.GetCurrentRentUnsafe(gameState, p))
                    .ToList();
                    
                if (theirProps.Count > 0)
                {
                    return theirProps[0].PositionIndex;
                }
            }
            return -1;
        }

        public override int SelectTargetForWorldTour(GameState gameState, GamePlayerState bot)
        {
            var availableProps = gameState.Properties.Values.Where(p => p.OwnerPlayerIndex < 0 && (p.Type == "City" || p.Type == "Resort")).ToList();
            if (availableProps.Count > 0 && bot.Money > 100000)
            {
                var target = availableProps.OrderByDescending(p => p.BuyPrice).FirstOrDefault(p => CheckCompletesColorSet(gameState, bot, p) || BlockOpponentColorSet(gameState, bot, p));
                if (target != null) return target.PositionIndex;
                return availableProps.OrderByDescending(p => p.BuyPrice).First().PositionIndex;
            }
            return 0; // START
        }
    }
}
