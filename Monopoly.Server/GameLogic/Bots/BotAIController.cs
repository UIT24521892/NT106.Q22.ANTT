using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monopoly.Server.Models;
using Monopoly.Server.Models.State;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Network;

namespace Monopoly.Server.GameLogic.Bots
{
    public static class BotAIController
    {
        public static async Task PlayBotTurnAsync(Room room, GamePlayerState bot)
        {
            Console.WriteLine($"[BOT] Lượt của bot {bot.Username} bắt đầu.");

            // 1. Nghỉ một chút trước khi làm gì đó (giả lập delay người chơi)
            await Task.Delay(1000);

            lock (ServerState.Lock)
            {
                // Kiểm tra lại trạng thái game xem còn hợp lệ không
                if (!room.IsStarted || room.GameState.IsFinished || room.GameState.CurrentTurnPlayerIndex != bot.PlayerIndex)
                    return;

                // Giai đoạn 1 & 2: Xử lý Đảo Hoang (Jail)
                if (bot.JailTurnsLeft > 0)
                {
                    if (bot.Money > 500000)
                    {
                        // Ra tù ngay lập tức
                        bot.JailTurnsLeft = 0;
                        bot.Money -= 500000;
                        string msg = $"{bot.Username} đã dùng 500,000 để ra tù.";
                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                        room.GameState.LastActionMessage = msg;
                    }
                    else
                    {
                        // Chờ đổ xúc xắc (hy vọng đôi)
                        string msg = $"{bot.Username} quyết định chờ đổ xúc xắc để ra tù.";
                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                        room.GameState.LastActionMessage = msg;
                    }
                }
            }
            
            // Broadcast trạng thái sau khi quyết định Đảo Hoang
            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);

            // Nghỉ trước khi đổ xúc xắc
            await Task.Delay(1500);

            List<CardDrawEvent> cardEvents = new List<CardDrawEvent>();
            bool hasRolledDouble = false;

            lock (ServerState.Lock)
            {
                if (!room.IsStarted || room.GameState.IsFinished || room.GameState.CurrentTurnPlayerIndex != bot.PlayerIndex)
                    return;

                // Đổ xúc xắc
                int dice1 = ServerState.Random.Next(1, 7);
                int dice2 = ServerState.Random.Next(1, 7);
                
                // Cheat xúc xắc nếu có thẻ (Giả định: Bot hiện tại dùng xúc xắc thật)
                // ...
                
                hasRolledDouble = (dice1 == dice2);
                room.GameState.HasRolledThisTurn = true;

                if (bot.JailTurnsLeft > 0)
                {
                    if (hasRolledDouble)
                    {
                        bot.JailTurnsLeft = 0;
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
                    GameEngine.MovePlayerByDiceUnsafe(room.GameState, bot, bot.Position, dice1, dice2, actionMessages, cardEvents);
                    
                    room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                    GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                }
            }

            // Gửi sự kiện thẻ nếu có
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

            // Giai đoạn mua/xây nhà và phá sản
            lock (ServerState.Lock)
            {
                if (!room.IsStarted || room.GameState.IsFinished || room.GameState.CurrentTurnPlayerIndex != bot.PlayerIndex)
                    return;

                // Xử lý nợ nần nếu tiền âm (Bankruptcy / Sell properties)
                if (bot.Money < 0)
                {
                    HandleBotDebtUnsafe(room.GameState, bot);
                }

                // Nếu vẫn còn sống sót
                if (!bot.IsBankrupt && bot.JailTurnsLeft <= 0)
                {
                    // Lấy ô hiện tại
                    if (room.GameState.Properties.TryGetValue(bot.Position, out GamePropertyState property))
                    {
                        if (property.Type == "City" || property.Type == "Resort")
                        {
                            if (property.OwnerPlayerIndex < 0) // Ô trống
                            {
                                // Quyết định mua
                                long safeBuffer = 100000;
                                bool completesColorSet = CompletesColorSet(room.GameState, bot, property);
                                
                                if (bot.Money - property.BuyPrice > safeBuffer || completesColorSet)
                                {
                                    if (bot.Money >= property.BuyPrice)
                                    {
                                        bot.Money -= property.BuyPrice;
                                        property.OwnerPlayerIndex = bot.PlayerIndex;
                                        string msg = $"{bot.Username} quyết định mua {property.Name} với giá {property.BuyPrice:N0}.";
                                        GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                        room.GameState.LastActionMessage = msg;
                                    }
                                }
                            }
                            else if (property.OwnerPlayerIndex == bot.PlayerIndex && property.Type == "City") // Ô của mình
                            {
                                // Quyết định xây nhà
                                long safeBuffer = 200000;
                                long buildCost = GameEngine.GetBuildCostUnsafe(property);
                                
                                if (buildCost > 0 && bot.Money - buildCost > safeBuffer)
                                {
                                    bot.Money -= buildCost;
                                    if (property.HouseCount < 3 && !property.HasHotel)
                                    {
                                        property.HouseCount++;
                                    }
                                    else if (property.HouseCount == 3 && !property.HasHotel)
                                    {
                                        property.HasHotel = true;
                                    }
                                    
                                    string lvl = GameEngine.DescribePropertyLevelUnsafe(property);
                                    string msg = $"{bot.Username} xây {lvl} tại {property.Name} với giá {buildCost:N0}.";
                                    GameEngine.AddGameLogUnsafe(room.GameState, msg);
                                    room.GameState.LastActionMessage = msg;
                                }
                            }
                        }
                    }
                }
            }

            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
            await Task.Delay(2500);

            // Chuyển lượt
            lock (ServerState.Lock)
            {
                if (!room.IsStarted || room.GameState.IsFinished || room.GameState.CurrentTurnPlayerIndex != bot.PlayerIndex)
                    return;

                if (!bot.IsBankrupt && hasRolledDouble && bot.ConsecutiveDoubles < 3 && bot.JailTurnsLeft <= 0)
                {
                    // Được đổ tiếp
                    room.GameState.HasRolledThisTurn = false;
                    GameEngine.ResetTurnTimerUnsafe(room.GameState);
                    string msg = $"{bot.Username} được đổ xúc xắc lần nữa do ra đôi!";
                    GameEngine.AddGameLogUnsafe(room.GameState, msg);
                    room.GameState.LastActionMessage = msg;
                    // Lượt tiếp theo sẽ do TurnTimer hoặc gọi đệ quy. 
                    // Để đơn giản, ta cho vòng lặp TurnTimer tự gọi lại hàm này ở chu kì sau nếu HasRolledThisTurn = false.
                }
                else
                {
                    // Hết lượt
                    GamePlayerState nextPlayer = GameEngine.GetNextTurnPlayerUnsafe(room.GameState);
                    room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                    room.GameState.CurrentTurnUsername = nextPlayer.Username;
                    room.GameState.HasRolledThisTurn = false;
                    
                    if (bot.ConsecutiveDoubles >= 3) bot.ConsecutiveDoubles = 0;

                    GameEngine.ResetTurnTimerUnsafe(room.GameState);
                    string msg = $"Lượt của {bot.Username} đã kết thúc. Tiếp theo là {nextPlayer.Username}.";
                    GameEngine.AddGameLogUnsafe(room.GameState, msg);
                    room.GameState.LastActionMessage = msg;
                }
            }

            await NetworkSender.BroadcastGameStateAsync(room.RoomId, room.GameState.LastActionMessage);
        }

        private static bool CompletesColorSet(GameState gameState, GamePlayerState bot, GamePropertyState targetProp)
        {
            if (string.IsNullOrEmpty(targetProp.ColorSet)) return false;
            
            var propertiesInSet = gameState.Properties.Values.Where(p => p.ColorSet == targetProp.ColorSet).ToList();
            int owned = propertiesInSet.Count(p => p.OwnerPlayerIndex == bot.PlayerIndex);
            
            // Nếu mua thêm ô này là đủ trọn bộ
            return (owned + 1) == propertiesInSet.Count;
        }

        private static void HandleBotDebtUnsafe(GameState gameState, GamePlayerState bot)
        {
            var botProps = gameState.Properties.Values.Where(p => p.OwnerPlayerIndex == bot.PlayerIndex).ToList();
            
            // Bán đất lẻ tẻ trước (Không thuộc set hoàn chỉnh)
            botProps = botProps.OrderBy(p => CompletesColorSet(gameState, bot, p)).ThenBy(p => p.BuyPrice).ToList();

            foreach (var prop in botProps)
            {
                if (bot.Money >= 0) break;

                // Bán giá một nửa (hoặc giá thanh lý tuỳ rule)
                long sellValue = prop.BuyPrice / 2;
                if (prop.HouseCount > 0) sellValue += (prop.HouseCount * 100000); // Giả định nhà
                if (prop.HasHotel) sellValue += 500000;

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
