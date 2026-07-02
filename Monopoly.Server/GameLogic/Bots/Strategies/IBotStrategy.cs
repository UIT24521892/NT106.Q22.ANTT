using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using System.Collections.Generic;

namespace Monopoly.Server.GameLogic.Bots.Strategies
{
    public interface IBotStrategy
    {
        bool ShouldBuyProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet);
        bool ShouldBuyoutProperty(GameState gameState, GamePlayerState bot, GamePropertyState property, out bool completesColorSet);
        bool ShouldBuildProperty(GameState gameState, GamePlayerState bot, GamePropertyState property);
        int SelectTargetForNegativeCard(GameState gameState, GamePlayerState bot, string cardType);
        int SelectTargetForPositiveCard(GameState gameState, GamePlayerState bot, string cardType);
        int SelectTargetForWorldTour(GameState gameState, GamePlayerState bot);
        int SelectTargetForWorldChampionship(GameState gameState, GamePlayerState bot);
    }
}
