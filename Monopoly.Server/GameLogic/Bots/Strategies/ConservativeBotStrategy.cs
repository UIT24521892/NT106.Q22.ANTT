using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public class ConservativeBotStrategy : BotStrategyBase
    {
        public override bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet)
        {
            completesColorSet = CheckCompletesColorSet(gameState, bot, property);
            bool blockOpponent = BlockOpponentColorSet(gameState, bot, property);
            
            long safeBuffer = 300000; // Rất cao
            return bot.Money - property.BuyPrice > safeBuffer || completesColorSet || blockOpponent;
        }

        public override bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property)
        {
            long safeBuffer = 400000;
            long buildCost = GameEngine.GetBuildCostUnsafe(property);
            return buildCost > 0 && bot.Money - buildCost > safeBuffer;
        }
    }
}
