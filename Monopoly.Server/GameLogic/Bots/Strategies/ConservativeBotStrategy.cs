using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using System.Linq;
using System.Collections.Generic;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public class ConservativeBotStrategy : BotStrategyBase
    {
        public override bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            bool blockOpponent = BlockOpponentColorSet(gameState, bot, property);
            
            long safeBuffer = 300000; 
            // Conservative only blocks opponent if it has at least 200k buffer
            return bot.Money - property.BuyPrice > safeBuffer || completesColorSet || (blockOpponent && bot.Money - property.BuyPrice > 200000);
        }

        public override bool ShouldBuyoutProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            long buyoutCost = GameEngine.GetBuyoutCostUnsafe(property);
            if (buyoutCost <= 0) return false;
            
            // Thận trọng: Chỉ mua nếu việc đó hoàn thành màu và vẫn dư cực kỳ nhiều tiền (> 500,000)
            return completesColorSet && bot.Money - buyoutCost > 500000;
        }
        public override bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property)
        {
            long safeBuffer = 400000;
            long buildCost = GameEngine.GetBuildCostUnsafe(property);
            return buildCost > 0 && bot.Money - buildCost > safeBuffer;
        }

        public override int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType)
        {
            // Conservative: Defensive, targets the most dangerous property overall on the board (highest rent) regardless of owner
            var dangerousProps = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex >= 0 && p.OwnerPlayerIndex != bot.PlayerIndex && p.Type == "City")
                .OrderByDescending(p => GameEngine.GetCurrentRentUnsafe(gameState, p))
                .ToList();
                
            if (dangerousProps.Count > 0)
            {
                return dangerousProps[0].PositionIndex;
            }
            return -1;
        }

        public override int SelectTargetForWorldTour(GameState gameState, GamePlayerState bot)
        {
            // Conservative: If money is low, go to start. Else buy cheapest property to save money
            if (bot.Money < 300000) return 0; // Go to START to get money
            
            var availableProps = gameState.Properties.Values.Where(p => p.OwnerPlayerIndex < 0 && (p.Type == "City" || p.Type == "Resort")).ToList();
            if (availableProps.Count > 0)
            {
                var target = availableProps.OrderBy(p => p.BuyPrice).FirstOrDefault(p => CheckCompletesColorSet(gameState, bot, p));
                if (target != null) return target.PositionIndex;
                
                // Buy cheapest
                return availableProps.OrderBy(p => p.BuyPrice).First().PositionIndex;
            }
            return 0; // START
        }
    }
}
