using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using System.Linq;
using System.Collections.Generic;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public class AggressiveBotStrategy : BotStrategyBase
    {
        public override bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            long safeBuffer = 10000; 
            // Aggressive bot cares about completing its set or just buying everything. Doesn't care much about blocking.
            // But occasionally borrow money if it can complete a set? We just use a low buffer.
            return bot.Money - property.BuyPrice > safeBuffer || completesColorSet;
        }

        public override bool ShouldBuyoutProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            long buyoutCost = GameEngine.GetBuyoutCostUnsafe(property);
            if (buyoutCost <= 0) return false;
            
            // Máu chiến: Mua nếu còn dư > 50,000, hoặc nếu hoàn thành được màu thì mua luôn không màng số dư!
            return bot.Money - buyoutCost > 50000 || completesColorSet;
        }
        public override bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property)
        {
            long safeBuffer = 50000;
            long buildCost = GameEngine.GetBuildCostUnsafe(property);
            return buildCost > 0 && bot.Money - buildCost > safeBuffer;
        }

        public override int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType)
        {
            // Aggressive: Target the richest player unconditionally
            var opponents = gameState.Players.Where(p => !p.IsBankrupt && p.PlayerIndex != bot.PlayerIndex).ToList();
            if (opponents.Count == 0) return -1;
            
            var richest = opponents.OrderByDescending(p => GameEngine.GetPlayerNetWorthUnsafe(gameState, p)).FirstOrDefault();
            
            if (richest != null)
            {
                // Target their most expensive property regardless of type
                var theirProps = gameState.Properties.Values
                    .Where(p => p.OwnerPlayerIndex == richest.PlayerIndex && (p.Type == "City" || p.Type == "Resort"))
                    .OrderByDescending(p => p.BuyPrice + p.HouseCount * 100000)
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
            if (availableProps.Count > 0)
            {
                // Buy the most expensive property immediately, ignoring money buffer
                var target = availableProps.OrderByDescending(p => p.BuyPrice).FirstOrDefault(p => CheckCompletesColorSet(gameState, bot, p));
                if (target != null) return target.PositionIndex;
                return availableProps.OrderByDescending(p => p.BuyPrice).First().PositionIndex;
            }
            return 0; // START
        }
    }
}
