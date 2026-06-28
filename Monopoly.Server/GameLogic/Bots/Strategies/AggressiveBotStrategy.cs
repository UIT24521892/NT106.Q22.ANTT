using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public class AggressiveBotStrategy : BotStrategyBase
    {
        public override bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            long safeBuffer = 10000; // Rất thấp
            return bot.Money - property.BuyPrice > safeBuffer || completesColorSet;
        }

        public override bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property)
        {
            long safeBuffer = 50000;
            long buildCost = GameEngine.GetBuildCostUnsafe(property);
            return buildCost > 0 && bot.Money - buildCost > safeBuffer;
        }
    }
}
