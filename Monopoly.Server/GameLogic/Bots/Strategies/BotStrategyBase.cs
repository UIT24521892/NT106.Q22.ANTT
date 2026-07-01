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
            if (targetProp.Type == "Resort")
            {
                int owned = gameState.Properties.Values.Count(p => p.Type == "Resort" && p.OwnerPlayerIndex == bot.PlayerIndex);
                return (owned + 1) >= 4;
            }
            
            if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
            
            var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
            int owned2 = propertiesInSet.Count(p => p.OwnerPlayerIndex == bot.PlayerIndex);
            
            return (owned2 + 1) == propertiesInSet.Count;
        }
        
        protected bool BlockOpponentColorSet(GameState gameState, GamePlayerState bot, GamePropertyState targetProp)
        {
            var otherPlayers = gameState.Players.Where(p => !p.IsBankrupt && p.PlayerIndex != bot.PlayerIndex).ToList();
            
            if (targetProp.Type == "Resort")
            {
                foreach (var op in otherPlayers)
                {
                    int opOwned = gameState.Properties.Values.Count(p => p.Type == "Resort" && p.OwnerPlayerIndex == op.PlayerIndex);
                    if (opOwned >= 3) return true; // Opponent is 1 resort away from Monopoly
                }
            }
            else
            {
                if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
                var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
                foreach (var op in otherPlayers)
                {
                    int opOwned = propertiesInSet.Count(p => p.OwnerPlayerIndex == op.PlayerIndex);
                    if (opOwned > 0 && opOwned + 1 == propertiesInSet.Count)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public virtual int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType)
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
        public virtual int SelectTargetForPositiveCard(GameState gameState, GamePlayerState bot, string cardType)
        {
            if (cardType == "FLIGHT")
            {
                // Hành vi giống Du lịch thế giới: bay đến ô tốt nhất
                return SelectTargetForWorldTour(gameState, bot);
            }
            if (cardType == "MOVE_CHAMPIONSHIP")
            {
                return SelectTargetForWorldChampionship(gameState, bot);
            }

            var myProps = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex == bot.PlayerIndex && p.Type == "City")
                .ToList();
                
            if (myProps.Count == 0) return -1;
            
            if (cardType == "FREE_UPGRADE")
            {
                var upgradeable = myProps.Where(p => p.HouseCount < 3 && !p.HasHotel).ToList();
                if (upgradeable.Count > 0)
                {
                    return upgradeable.OrderByDescending(p => p.BuyPrice).First().PositionIndex;
                }
            }
            return -1;
        }

        public virtual int SelectTargetForWorldTour(GameState gameState, GamePlayerState bot)
        {
            var availableProps = gameState.Properties.Values.Where(p => p.OwnerPlayerIndex < 0 && (p.Type == "City" || p.Type == "Resort")).ToList();
            if (availableProps.Count > 0 && bot.Money > 2000000)
            {
                var target = availableProps.OrderByDescending(p => p.BuyPrice).FirstOrDefault(p => CheckCompletesColorSet(gameState, bot, p));
                if (target != null) return target.PositionIndex;
                return availableProps.OrderByDescending(p => p.BuyPrice).First().PositionIndex;
            }
            return 0; // Go to start
        }

        public virtual int SelectTargetForWorldChampionship(GameState gameState, GamePlayerState bot)
        {
            var myProps = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex == bot.PlayerIndex && (p.Type == "City" || p.Type == "Resort"))
                .ToList();
            if (myProps.Count > 0)
            {
                return myProps.OrderByDescending(p => GameEngine.GetCurrentRentUnsafe(gameState, p)).First().PositionIndex;
            }
            return -1; // If no properties, maybe just pick current pos
        }
    }
}
