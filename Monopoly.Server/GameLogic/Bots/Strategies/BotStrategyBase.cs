using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using System.Linq;
using System.Collections.Generic;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public abstract class BotStrategyBase : IBotStrategy
    {
        public abstract bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet);
        public abstract bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property);

        protected bool CheckCompletesColorSet(GameState gameState, GamePlayerState bot, GamePropertyState targetProp)
        {
            if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
            
            var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
            int owned = propertiesInSet.Count(p => p.OwnerPlayerIndex == bot.PlayerIndex);
            
            return (owned + 1) == propertiesInSet.Count;
        }
        
        protected bool BlockOpponentColorSet(GameState gameState, GamePlayerState bot, GamePropertyState targetProp)
        {
            if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
            
            var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
            
            // Check if any other player is 1 away from completing this set
            var otherPlayers = gameState.Players.Where(p => !p.IsBankrupt && p.PlayerIndex != bot.PlayerIndex).ToList();
            foreach (var op in otherPlayers)
            {
                int opOwned = propertiesInSet.Count(p => p.OwnerPlayerIndex == op.PlayerIndex);
                if (opOwned > 0 && opOwned + 1 == propertiesInSet.Count)
                {
                    return true;
                }
            }
            return false;
        }

        public int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType)
        {
            // Find player with highest net worth or most expensive properties
            var opponents = gameState.Players.Where(p => !p.IsBankrupt && p.PlayerIndex != bot.PlayerIndex).ToList();
            if (opponents.Count == 0) return -1;
            
            // Simple target: highest net worth
            var richest = opponents.OrderByDescending(p => GameEngine.GetPlayerNetWorthUnsafe(gameState, p)).FirstOrDefault();
            
            if (richest != null)
            {
                // Find their most expensive property
                var theirProps = gameState.Properties.Values
                    .Where(p => p.OwnerPlayerIndex == richest.PlayerIndex && p.Type == "City")
                    .OrderByDescending(p => GameEngine.GetCurrentRentUnsafe(gameState, p))
                    .ToList();
                    
                if (theirProps.Count > 0)
                {
                    return theirProps[0].PositionIndex;
                }
            }
            return -1;
        }
    }
}
