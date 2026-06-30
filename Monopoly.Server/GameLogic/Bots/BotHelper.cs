using System;
using System.Collections.Generic;
using System.Linq;
using Monopoly.Server.Models.State;
using Monopoly.Shared.Models.Network.Payloads;

namespace Monopoly.Server.GameLogic.Bots
{
    public static class BotHelper
    {
        public static void AutoSellPropertiesForDebtUnsafe(
                GameState gameState,
                GamePlayerState bot,
                long remainingDebt,
                int creditorPlayerIndex,
                string debtReason,
                List<string> actionMessages)
        {
            List<GamePropertyState> botProperties = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex == bot.PlayerIndex)
                .OrderBy(GameEngine.GetPropertySaleValueUnsafe)
                .ToList();

            foreach (GamePropertyState property in botProperties)
            {
                if (remainingDebt <= 0)
                    break;

                long saleValue = GameEngine.GetPropertySaleValueUnsafe(property);
                string propertyName = property.Name;
                GameEngine.ReleasePropertyUnsafe(property);

                long paidToDebt = Math.Min(saleValue, remainingDebt);
                remainingDebt -= paidToDebt;
                GameEngine.PayDebtRecipientUnsafe(gameState, creditorPlayerIndex, paidToDebt);

                long surplus = saleValue - paidToDebt;

                if (surplus > 0)
                    bot.Money += surplus;

                actionMessages.Add($"{bot.Username} tự bán {propertyName} thu {saleValue:N0} để trả {debtReason}.");
            }

            if (remainingDebt > 0)
            {
                actionMessages.Add($"{bot.Username} vẫn còn nợ {remainingDebt:N0} sau khi thanh lý tài sản.");
                GameEngine.HandleBankruptcyUnsafe(gameState, bot, actionMessages);
            }
        }
    }
}
