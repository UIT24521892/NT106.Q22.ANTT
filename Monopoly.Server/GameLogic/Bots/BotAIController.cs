using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monopoly.Server.Models;
using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic.Bots.Strategies;

namespace Monopoly.Server.GameLogic.Bots
{
    public static class BotAIController
    {
        private static IBotStrategy GetStrategyForBot(GamePlayerState bot)
        {
            switch (bot.Personality)
            {
                case BotPersonality.Aggressive:
                    return new AggressiveBotStrategy();
                case BotPersonality.Conservative:
                    return new ConservativeBotStrategy();
                case BotPersonality.Balanced:
                default:
                    return new BalancedBotStrategy();
            }
        }

        public static async Task PlayBotTurnAsync(Room room, GamePlayerState bot)
        {
            Console.WriteLine($"[BOT] Lượt của bot {bot.Username} ({bot.Personality}) bắt đầu.");

            await Task.Delay(1000);

            lock (ServerState.Lock)
            {
                if (!CanContinueTurnUnsafe(room, bot))
                    return;

                if (bot.JailTurnsLeft > 0)
                {
                    bool shouldPay = bot.Money > 500000 || bot.HasEscapeIslandCard;
                    
                    if (bot.HasEscapeIslandCard)
                    {
                        bot.JailTurnsLeft = 0;
                        bot.HasEscapeIslandCard = false;
                        string msg = $"{bot.Username} đã dùng Thẻ Ra Tù Miễn Phí.";
                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                        room.GameState.LastActionMessage = msg;
                    }
                    else if (bot.Money > 500000)
                    {
                        bot.JailTurnsLeft = 0;
                        bot.Money -= 500000;
                        string msg = $"{bot.Username} đã dùng 500,000 để ra tù.";
                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                        room.GameState.LastActionMessage = msg;
                    }
                    else
                    {
                        string msg = $"{bot.Username} quyết định chờ đổ xúc xắc để ra tù.";
                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                        room.GameState.LastActionMessage = msg;
                    }
                }
            }
            
            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
            await Task.Delay(1500);

            List<CardDrawEvent> cardEvents = new List<CardDrawEvent>();
            bool hasRolledDouble = false;
            bool wasInJail = false;

            lock (ServerState.Lock)
            {
                if (!CanContinueTurnUnsafe(room, bot))
                    return;

                int dice1 = ServerState.Random.Next(1, 7);
                int dice2 = ServerState.Random.Next(1, 7);
                
                if (bot.HasForceDoubleCard && bot.JailTurnsLeft > 0)
                {
                    bot.HasForceDoubleCard = false;
                    dice1 = ServerState.Random.Next(1, 7);
                    dice2 = dice1;
                    GameEngine.AddGameLogUnsafe(room.GameState, $"{bot.Username} đã dùng Thẻ Ép Đổ Đôi để ra tù!");
                }
                
                hasRolledDouble = (dice1 == dice2);
                wasInJail = bot.JailTurnsLeft > 0;
                room.GameState.HasRolledThisTurn = true;

                if (wasInJail)
                {
                    if (hasRolledDouble)
                    {
                        bot.JailTurnsLeft = 0;
                        bot.IsOnIsland = false;
                        GameEngine.AddGameLogUnsafe(room.GameState, $"{bot.Username} đã đổ được đôi ({dice1}-{dice2}) và thoát khỏi Đảo Hoang!");
                    }
                    else
                    {
                        bot.JailTurnsLeft--;
                        GameEngine.AddGameLogUnsafe(room.GameState, $"{bot.Username} đổ ({dice1}-{dice2}) không phải đôi, vẫn ở Đảo Hoang.");
                    }
                }

                if (bot.JailTurnsLeft <= 0)
                {
                    List<string> actionMessages = new List<string>();
                    bool sentToIslandByDoubles = false;

                    if (!wasInJail && hasRolledDouble)
                    {
                        bot.ConsecutiveDoubles++;
                        if (bot.ConsecutiveDoubles >= 3)
                        {
                            bot.ConsecutiveDoubles = 0;
                            GameEngine.SendPlayerToIslandUnsafe(bot);
                            room.GameState.LastFinalPosition = bot.Position;
                            sentToIslandByDoubles = true;
                            room.GameState.LastActionMessage = $"{bot.Username} đổ đôi 3 lần liên tiếp và bị đưa thẳng vào Đảo Hoang!";
                            GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                        }
                    }
                    else if (!wasInJail)
                    {
                        bot.ConsecutiveDoubles = 0;
                    }

                    if (!sentToIslandByDoubles)
                    {
                        GameEngine.MovePlayerByDiceUnsafe(room.GameState, bot, bot.Position, dice1, dice2, actionMessages, cardEvents);
                        room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                    }
                }
            }

            if (cardEvents.Count > 0)
            {
                foreach (var evt in cardEvents)
                {
                    await NetworkSender.BroadcastCardDrawnAsync(room.RoomId, evt);
                    await Task.Delay(2500);
                }
            }

            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
            await Task.Delay(4000);

            lock (ServerState.Lock)
            {
                if (!CanContinueTurnUnsafe(room, bot))
                    return;

                if (bot.Money < 0)
                {
                    HandleBotDebtUnsafe(room.GameState, bot);
                }
                
                if (!bot.IsBankrupt && bot.JailTurnsLeft <= 0)
                {
                    var strategy = GetStrategyForBot(bot);

                    if (room.GameState.Properties.TryGetValue(bot.Position, out GamePropertyState property))
                    {
                        if (property.Type == "City" || property.Type == "Resort")
                        {
                            if (property.OwnerPlayerIndex < 0) 
                            {
                                if (strategy.ShouldBuyProperty(room.GameState, bot, property, out bool completesColorSet))
                                {
                                    if (GameEngine.TryBuyPropertyUnsafe(room.GameState, bot, property, out string err))
                                    {
                                        string msg = $"{bot.Username} quyết định mua {property.Name} với giá {property.BuyPrice:N0}.";
                                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                        room.GameState.LastActionMessage = msg;
                                    }
                                }
                            }
                            else if (property.OwnerPlayerIndex == bot.PlayerIndex && property.Type == "City") 
                            {
                                if (strategy.ShouldBuildProperty(room.GameState, bot, property))
                                {
                                    // Use free upgrade card if we have it
                                    if (bot.HasFreeUpgradeCard)
                                    {
                                        if (GameEngine.TryApplyHeldCardEffectUnsafe(room.GameState, bot, "FREE_UPGRADE", null, new List<string>(), new List<CardDrawEvent>(), out string err2))
                                        {
                                            string msg = $"{bot.Username} đã dùng thẻ Nâng Cấp Miễn Phí tại {property.Name}!";
                                            GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                            room.GameState.LastActionMessage = msg;
                                        }
                                    }
                                    else if (GameEngine.TryBuildPropertyUnsafe(room.GameState, bot, property, out string err))
                                    {
                                        string lvl = GameEngine.DescribePropertyLevelUnsafe(property);
                                        long buildCost = GameEngine.GetBuildCostUnsafe(property);
                                        string msg = $"{bot.Username} xây {lvl} tại {property.Name} với giá {buildCost:N0}.";
                                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                        room.GameState.LastActionMessage = msg;
                                    }
                                }
                            }
                            else if (property.OwnerPlayerIndex != bot.PlayerIndex)
                            {
                                // We are on someone else's property. We might owe rent.
                                // The MovePlayerByDiceUnsafe already deducted rent! 
                                // Wait, the rent was already deducted during move. If we had FreeRentCard, we should have used it.
                                // Actually, rent is deducted during HandleBankruptcy or inside MovePlayerByDiceUnsafe if they have enough money.
                                // We can't retroactively apply free rent easily here if rent was already deducted.
                                // To fix this properly without refactoring GameEngine entirely, we can just assume if we are here and we HAVE FreeRentCard, we use it to get a refund or similar.
                                // Or we just use it if we are on an opponent's property.
                                if (bot.HasFreeRentCard)
                                {
                                    // TryApplyHeldCardEffectUnsafe will process the card.
                                    if (GameEngine.TryApplyHeldCardEffectUnsafe(room.GameState, bot, "FREE_RENT", null, new List<string>(), new List<CardDrawEvent>(), out string err))
                                    {
                                        string msg = $"{bot.Username} đã dùng thẻ Miễn Tiền Thuê.";
                                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                        room.GameState.LastActionMessage = msg;
                                    }
                                }
                            }
                        }
                    }

                    // Smart use of negative cards
                    if (bot.HasEarthquakeCard)
                    {
                        int targetPos = strategy.SelectTargetForNegativeCard(room.GameState, bot, "EARTHQUAKE");
                        if (targetPos >= 0)
                        {
                            if (GameEngine.TryApplyHeldCardEffectUnsafe(room.GameState, bot, "EARTHQUAKE", targetPos, new List<string>(), new List<CardDrawEvent>(), out string err))
                            {
                                string msg = $"{bot.Username} đã dùng thẻ Động Đất lên ô {targetPos}.";
                                GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                room.GameState.LastActionMessage = msg;
                            }
                        }
                    }
                    
                    if (bot.HasPowerOutageCard)
                    {
                        int targetPos = strategy.SelectTargetForNegativeCard(room.GameState, bot, "POWER_OUTAGE");
                        if (targetPos >= 0)
                        {
                            if (GameEngine.TryApplyHeldCardEffectUnsafe(room.GameState, bot, "POWER_OUTAGE", targetPos, new List<string>(), new List<CardDrawEvent>(), out string err))
                            {
                                string msg = $"{bot.Username} đã dùng thẻ Mất Điện lên ô {targetPos}.";
                                GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                room.GameState.LastActionMessage = msg;
                            }
                        }
                    }
                }
            }

            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
            await Task.Delay(2500);

            lock (ServerState.Lock)
            {
                if (!CanContinueTurnUnsafe(room, bot))
                    return;

                if (!bot.IsBankrupt && hasRolledDouble && !wasInJail && bot.ConsecutiveDoubles < 3 && bot.JailTurnsLeft <= 0)
                {
                    room.GameState.HasRolledThisTurn = false;
                    GameEngine.ResetTurnTimerUnsafe(room.GameState);
                    string msg = $"{bot.Username} được đổ xúc xắc lần nữa do ra đôi!";
                    GameEngine.AddGameLogUnsafe(room.GameState, msg);
                    room.GameState.LastActionMessage = msg;
                }
                else
                {
                    GameEngine.StartNextTurnUnsafe(room.GameState, out GamePlayerState? nextPlayer);
                    GameEngine.ResetTurnTimerUnsafe(room.GameState);
                    string msg = $"Lượt của {bot.Username} đã kết thúc. Tiếp theo là {nextPlayer?.Username}.";
                    GameEngine.AddGameLogUnsafe(room.GameState, msg);
                    room.GameState.LastActionMessage = msg;
                }
            }

            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
        }

        private static bool CanContinueTurnUnsafe(Room room, GamePlayerState bot)
        {
            return room != null &&
                room.IsStarted &&
                room.GameState != null &&
                !room.GameState.IsFinished &&
                !room.GameState.IsPaused &&
                room.GameState.CurrentTurnPlayerIndex == bot.PlayerIndex;
        }

        private static bool CompletesColorSet(GameState gameState, GamePlayerState bot, GamePropertyState targetProp)
        {
            if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
            
            var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
            int owned = propertiesInSet.Count(p => p.OwnerPlayerIndex == bot.PlayerIndex);
            
            return (owned + 1) == propertiesInSet.Count;
        }
        
        private static void HandleBotDebtUnsafe(GameState gameState, GamePlayerState bot)
        {
            var botProps = gameState.Properties.Values.Where(p => p.OwnerPlayerIndex == bot.PlayerIndex).ToList();
            botProps = botProps.OrderBy(p => CompletesColorSet(gameState, bot, p)).ThenBy(p => p.BuyPrice).ToList();

            foreach (var prop in botProps)
            {
                if (bot.Money >= 0) break;

                long sellValue = GameEngine.GetPropertySaleValueUnsafe(prop);
                bot.Money += sellValue;
                prop.OwnerPlayerIndex = -1;
                prop.HouseCount = 0;
                prop.HasHotel = false;

                GameEngine.AddGameLogUnsafe(gameState, $"{bot.Username} đã bán {prop.Name} để trả nợ, thu về {sellValue:N0}.");
            }

            if (bot.Money < 0)
            {
                List<string> msgs = new List<string>();
                GameEngine.HandleBankruptcyUnsafe(gameState, bot, msgs);
                GameEngine.AddGameLogUnsafe(gameState, $"{bot.Username} đã PHÁ SẢN do không đủ tiền trả nợ. " + string.Join(" ", msgs));
            }
        }
    }
}
