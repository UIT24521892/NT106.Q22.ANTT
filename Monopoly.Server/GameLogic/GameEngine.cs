using Monopoly.Server.Models;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Models.State;
using Monopoly.Shared.Models.Configs.Models;
using Monopoly.Shared.Models.Configs.StaticData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Services;
using Monopoly.Server.Network;


namespace Monopoly.Server.GameLogic
{
    public static class GameEngine
    {
        public static void ResetTurnTimerUnsafe(GameState gameState)
        {
            if (gameState.IsPaused)
                return;
            gameState.TurnDurationSeconds = 45;
            gameState.TurnEndsAtUtcTicks = DateTime.UtcNow.AddSeconds(45).Ticks;
            gameState.ServerUtcTicks = DateTime.UtcNow.Ticks;
        }
        public static void ApplySpecialSquareEffectUnsafe(
                GameState gameState,
                GamePlayerState player,
                int position,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents)
        {
            if (!BoardDatabase.Squares.TryGetValue(position, out var square))
            {
                return;
            }

            bool isChampionshipPosition = position == gameState.WorldChampionshipPosition;

            switch (square.Type)
            {
                case "Tax":
                    const long taxAmount = 100000;
                    ChargePlayerForDebtUnsafe(
                        gameState,
                        player,
                        taxAmount,
                        -1,
                        "thuế",
                        actionMessages);
                    break;

                case "Chance":
                    ApplyChanceEffectUnsafe(gameState, player, actionMessages, cardDrawEvents);
                    break;

                case "LostIsland":
                    SendPlayerToIslandUnsafe(player);
                    actionMessages.Add($"{player.Username} vào Đảo Hoang và có 3 lượt để lắc đôi thoát ra.");
                    break;

                case "WorldChampionship":
                    ApplyWorldChampionshipUnsafe(gameState, player, actionMessages);
                    isChampionshipPosition = false;

                    bool hasCity = gameState.Properties.Values.Any(p => p.Type == "City" && p.OwnerPlayerIndex == player.PlayerIndex);
                    if (hasCity && !player.IsBot)
                    {
                        gameState.IsWaitingForCardChoice = true;
                        gameState.PendingCardEffectCode = "WORLD_CHAMPIONSHIP_HOST";
                        gameState.PendingCardPlayerUsername = player.Username;
                        gameState.PendingCardTargetPositions = BuildCardTargetPositionsUnsafe(gameState, player, "WORLD_CHAMPIONSHIP_HOST");
                        actionMessages.Add($"{player.Username} đang chọn nơi đăng cai Giải Vô Địch.");
                    }
                    break;

                case "WorldTour":
                    if (!player.IsBot)
                    {
                        gameState.IsWaitingForCardChoice = true;
                        gameState.PendingCardEffectCode = "WORLD_TOUR";
                        gameState.PendingCardPlayerUsername = player.Username;
                        gameState.PendingCardTargetPositions = BuildCardTargetPositionsUnsafe(gameState, player, "WORLD_TOUR");
                        actionMessages.Add($"{player.Username} đang chọn điểm đến Du Lịch Thế Giới.");
                    }
                    else
                    {
                        player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                        player.SkipReason = "WORLD_TOUR";
                        actionMessages.Add($"{player.Username} đến Du Lịch Thế Giới và đợi đi tiếp.");
                    }
                    break;
            }

            if (isChampionshipPosition)
            {
                ApplyWorldChampionshipUnsafe(gameState, player, actionMessages);
            }
        }
        public static void MovePlayerByDiceUnsafe(
                GameState gameState,
                GamePlayerState player,
                int oldPosition,
                int dice1,
                int dice2,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents)
        {
            int boardSize = BoardDatabase.Squares.Count;
            int diceTotal = dice1 + dice2;
            int rawPosition = oldPosition + diceTotal;
            int diceLandingPosition = rawPosition % boardSize;

            actionMessages.Add(
                $"{player.Username} đi từ ô {oldPosition} đến ô {diceLandingPosition}."
            );

            player.Position = diceLandingPosition;

            if (rawPosition >= boardSize)
            {
                const long startBonus = 200000;
                player.Money += startBonus;
                actionMessages.Add($"{player.Username} đi qua Bắt Đầu và nhận {startBonus:N0}.");
            }

            ApplyRentOnLandingUnsafe(gameState, player, diceLandingPosition, actionMessages);
            ApplySpecialSquareEffectUnsafe(gameState, player, diceLandingPosition, actionMessages, cardDrawEvents);

            gameState.LastDice1 = dice1;
            gameState.LastDice2 = dice2;
            gameState.LastDiceTotal = diceTotal;
            gameState.LastMovedPlayerIndex = player.PlayerIndex;
            gameState.LastMoveFromPosition = oldPosition;
            gameState.LastMoveToPosition = diceLandingPosition;
            gameState.LastFinalPosition = player.Position;

            Console.WriteLine(
                $"[DICE] Room={gameState.RoomId}, Player={player.Username}, " +
                $"Dice={dice1}+{dice2}, DiceLanding={oldPosition}->{diceLandingPosition}, FinalPos={player.Position}, Money={player.Money}"
            );
        }
        public static void ApplyRentOnLandingUnsafe(
                GameState gameState,
                GamePlayerState player,
                int position,
                List<string> actionMessages)
        {
            if (!gameState.Properties.TryGetValue(position, out GamePropertyState landedProperty) ||
                landedProperty.OwnerPlayerIndex < 0 ||
                landedProperty.OwnerPlayerIndex == player.PlayerIndex)
            {
                return;
            }

            GamePlayerState owner = gameState.Players.FirstOrDefault(
                p => p.PlayerIndex == landedProperty.OwnerPlayerIndex
            );

            if (owner == null ||
                !BoardDatabase.Squares.TryGetValue(position, out var landedSquare) ||
                landedSquare.RentPrices.Count == 0)
            {
                return;
            }

            if (landedProperty.PowerOutageTurn >= gameState.TurnNumber)
            {
                actionMessages.Add($"{landedProperty.Name} đang mất điện, miễn tiền thuê lượt này.");
                return;
            }

            if (player.IsFreeRentShieldActive)
            {
                player.IsFreeRentShieldActive = false;
                actionMessages.Add($"{player.Username} dùng Khiên Miễn Trừ, không phải trả tiền thuê {landedProperty.Name}.");
                return;
            }

            long rent = GetCurrentRentUnsafe(gameState, landedProperty);
            ChargePlayerForDebtUnsafe(
                gameState,
                player,
                rent,
                owner.PlayerIndex,
                $"tiền thuê {landedProperty.Name} cho {owner.Username}",
                actionMessages);

            Console.WriteLine(
                $"[RENT] Room={gameState.RoomId}, From={player.Username}, To={owner.Username}, " +
                $"Property={landedProperty.Name}, Rent={rent}, " +
                $"PayerMoney={player.Money}, OwnerMoney={owner.Money}"
            );
        }
        public static void ChargePlayerForDebtUnsafe(
                GameState gameState,
                GamePlayerState player,
                long amount,
                int creditorPlayerIndex,
                string debtReason,
                List<string> actionMessages)
        {
            if (gameState == null || player == null || amount <= 0 || player.IsBankrupt)
                return;

            long cashAvailable = Math.Max(0, player.Money);

            if (cashAvailable >= amount)
            {
                player.Money -= amount;
                PayDebtRecipientUnsafe(gameState, creditorPlayerIndex, amount);
                actionMessages.Add($"{player.Username} trả {amount:N0} {debtReason}.");
                return;
            }

            long paidByCash = cashAvailable;

            if (paidByCash > 0)
            {
                player.Money -= paidByCash;
                PayDebtRecipientUnsafe(gameState, creditorPlayerIndex, paidByCash);
            }

            long remainingDebt = amount - paidByCash;
            long totalSaleValue = GetTotalPropertySaleValueUnsafe(gameState, player.PlayerIndex);

            if (totalSaleValue < remainingDebt)
            {
                actionMessages.Add(
                    $"{player.Username} chỉ trả được {paidByCash:N0}/{amount:N0} {debtReason}; tổng giá trị thanh lý tài sản {totalSaleValue:N0} không đủ trả nợ."
                );
                HandleBankruptcyUnsafe(gameState, player, actionMessages);
                return;
            }

            if (player.IsBot)
            {
                SellBotPropertiesForDebtUnsafe(gameState, player, remainingDebt, creditorPlayerIndex, debtReason, actionMessages);
                return;
            }

            gameState.IsWaitingForPropertySale = true;
            gameState.PendingSalePlayerIndex = player.PlayerIndex;
            gameState.PendingSalePlayerUsername = player.Username;
            gameState.PendingDebtAmount = remainingDebt;
            gameState.PendingDebtCreditorPlayerIndex = creditorPlayerIndex;
            gameState.PendingDebtReason = debtReason;
            RefreshPendingSalePositionsUnsafe(gameState);
            gameState.TurnEndsAtUtcTicks = 0;
            gameState.ServerUtcTicks = DateTime.UtcNow.Ticks;

            actionMessages.Add(
                $"{player.Username} trả trước {paidByCash:N0}/{amount:N0} {debtReason} và cần bán tài sản để trả còn thiếu {remainingDebt:N0}."
            );
        }

        public static bool TrySellPropertyForDebtUnsafe(
                GameState gameState,
                GamePlayerState player,
                int positionIndex,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (gameState == null || player == null)
            {
                failMessage = "Không tìm thấy trạng thái nợ.";
                return false;
            }

            if (!gameState.IsWaitingForPropertySale)
            {
                failMessage = "Không có khoản nợ nào đang chờ bán tài sản.";
                return false;
            }

            if (gameState.PendingSalePlayerIndex != player.PlayerIndex)
            {
                failMessage = "Không phải lượt bán tài sản của bạn.";
                return false;
            }

            if (!gameState.Properties.TryGetValue(positionIndex, out GamePropertyState property) ||
                property.OwnerPlayerIndex != player.PlayerIndex ||
                !gameState.PendingSalePropertyPositions.Contains(positionIndex))
            {
                failMessage = "Tài sản này không hợp lệ để bán trả nợ.";
                return false;
            }

            long saleValue = GetPropertySaleValueUnsafe(property);

            if (saleValue <= 0)
            {
                failMessage = "Tài sản này không có giá trị thanh lý hợp lệ.";
                return false;
            }

            string propertyName = property.Name;
            ReleasePropertyUnsafe(property);

            long paidToDebt = Math.Min(saleValue, gameState.PendingDebtAmount);
            gameState.PendingDebtAmount -= paidToDebt;
            PayDebtRecipientUnsafe(gameState, gameState.PendingDebtCreditorPlayerIndex, paidToDebt);

            long surplus = saleValue - paidToDebt;

            if (surplus > 0)
                player.Money += surplus;

            actionMessages.Add(
                $"{player.Username} bán {propertyName} thu {saleValue:N0}, trả nợ {paidToDebt:N0}" +
                (surplus > 0 ? $", còn dư {surplus:N0}." : ".")
            );

            if (gameState.PendingDebtAmount <= 0)
            {
                string reason = gameState.PendingDebtReason;
                ClearPendingPropertySaleUnsafe(gameState);
                ResetTurnTimerUnsafe(gameState);
                actionMessages.Add($"{player.Username} đã trả xong khoản {reason}.");
                return true;
            }

            RefreshPendingSalePositionsUnsafe(gameState);

            if (gameState.PendingSalePropertyPositions.Count == 0)
            {
                actionMessages.Add($"{player.Username} không còn tài sản để bán nhưng vẫn nợ {gameState.PendingDebtAmount:N0}.");
                ClearPendingPropertySaleUnsafe(gameState);
                HandleBankruptcyUnsafe(gameState, player, actionMessages);
            }
            else
            {
                actionMessages.Add($"{player.Username} còn nợ {gameState.PendingDebtAmount:N0}, cần bán thêm tài sản.");
            }

            return true;
        }

        public static long GetPropertySaleValueUnsafe(GamePropertyState property)
        {
            if (property == null || property.BuyPrice <= 0 || (property.Type != "City" && property.Type != "Resort"))
                return 0;

            long saleValue = Math.Max(1, property.BuyPrice / 2);

            if (property.Type == "City")
            {
                long houseBuildCost = Math.Max(1, property.BuyPrice / 2);
                saleValue += property.HouseCount * Math.Max(1, houseBuildCost / 2);

                if (property.HasHotel)
                    saleValue += Math.Max(1, property.BuyPrice / 2);
            }

            return saleValue;
        }

        public static long GetTotalPropertySaleValueUnsafe(GameState gameState, int ownerPlayerIndex)
        {
            if (gameState == null || gameState.Properties == null)
                return 0;

            long total = 0;

            foreach (GamePropertyState property in gameState.Properties.Values)
            {
                if (property.OwnerPlayerIndex == ownerPlayerIndex)
                    total += GetPropertySaleValueUnsafe(property);
            }

            return total;
        }

        public static void ClearPendingPropertySaleUnsafe(GameState gameState)
        {
            if (gameState == null)
                return;

            gameState.IsWaitingForPropertySale = false;
            gameState.PendingSalePlayerIndex = -1;
            gameState.PendingSalePlayerUsername = "";
            gameState.PendingDebtAmount = 0;
            gameState.PendingDebtCreditorPlayerIndex = -1;
            gameState.PendingDebtReason = "";
            gameState.PendingSalePropertyPositions.Clear();
        }

        private static void RefreshPendingSalePositionsUnsafe(GameState gameState)
        {
            gameState.PendingSalePropertyPositions.Clear();

            foreach (GamePropertyState property in gameState.Properties.Values
                         .Where(p => p.OwnerPlayerIndex == gameState.PendingSalePlayerIndex)
                         .OrderBy(p => p.PositionIndex))
            {
                if (GetPropertySaleValueUnsafe(property) > 0)
                    gameState.PendingSalePropertyPositions.Add(property.PositionIndex);
            }
        }

        private static void SellBotPropertiesForDebtUnsafe(
                GameState gameState,
                GamePlayerState bot,
                long remainingDebt,
                int creditorPlayerIndex,
                string debtReason,
                List<string> actionMessages)
        {
            List<GamePropertyState> botProperties = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex == bot.PlayerIndex)
                .OrderBy(GetPropertySaleValueUnsafe)
                .ToList();

            foreach (GamePropertyState property in botProperties)
            {
                if (remainingDebt <= 0)
                    break;

                long saleValue = GetPropertySaleValueUnsafe(property);
                string propertyName = property.Name;
                ReleasePropertyUnsafe(property);

                long paidToDebt = Math.Min(saleValue, remainingDebt);
                remainingDebt -= paidToDebt;
                PayDebtRecipientUnsafe(gameState, creditorPlayerIndex, paidToDebt);

                long surplus = saleValue - paidToDebt;

                if (surplus > 0)
                    bot.Money += surplus;

                actionMessages.Add($"{bot.Username} tự bán {propertyName} thu {saleValue:N0} để trả {debtReason}.");
            }

            if (remainingDebt > 0)
            {
                actionMessages.Add($"{bot.Username} vẫn còn nợ {remainingDebt:N0} sau khi thanh lý tài sản.");
                HandleBankruptcyUnsafe(gameState, bot, actionMessages);
            }
        }

        private static void PayDebtRecipientUnsafe(GameState gameState, int creditorPlayerIndex, long amount)
        {
            if (amount <= 0 || creditorPlayerIndex < 0)
                return;

            GamePlayerState creditor = gameState.Players.FirstOrDefault(p => p.PlayerIndex == creditorPlayerIndex);

            if (creditor != null && !creditor.IsBankrupt)
                creditor.Money += amount;
        }

        private static void ReleasePropertyUnsafe(GamePropertyState property)
        {
            property.OwnerPlayerIndex = -1;
            property.HouseCount = 0;
            property.HasHotel = false;
            property.Multiplier = 1;
            property.PowerOutageTurn = 0;
        }
        public static void SendPlayerToIslandUnsafe(GamePlayerState player)
        {
            player.Position = 24;
            player.IsOnIsland = true;
            player.JailTurnsLeft = Math.Max(player.JailTurnsLeft, 3);
            player.SkipTurnsLeft = 0;
            player.SkipReason = "";
        }
        public static void ApplyWorldChampionshipUnsafe(
                GameState gameState,
                GamePlayerState winner,
                List<string> actionMessages)
        {
            const long championshipFee = 100000;
            long totalCollected = 0;

            foreach (GamePlayerState other in gameState.Players)
            {
                if (other == null ||
                    other.PlayerIndex == winner.PlayerIndex ||
                    other.IsBankrupt)
                {
                    continue;
                }

                long amount = Math.Min(championshipFee, Math.Max(0, other.Money));

                if (amount <= 0)
                    continue;

                other.Money -= amount;
                winner.Money += amount;
                totalCollected += amount;
            }

            if (totalCollected > 0)
            {
                actionMessages.Add($"{winner.Username} thắng Giải Vô Địch và thu {totalCollected:N0} từ các đối thủ.");
            }
            else
            {
                actionMessages.Add($"{winner.Username} đến Giải Vô Địch nhưng không thu được tiền.");
            }
        }
        public static void ApplyChanceEffectUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents)
        {
            ChanceCardConfig card = ServerState.DeckManager.DrawCard();

            if (card == null)
            {
                actionMessages.Add($"{player.Username} rút thẻ Cơ Hội nhưng không có dữ liệu thẻ.");
                return;
            }

            player.LastDrawnCardId = card.ID;
            cardDrawEvents.Add(new CardDrawEvent
            {
                DrawnByUsername = player.Username,
                CardId = card.ID,
                CardName = card.Name,
                CardType = card.Type,
                DetailEffect = card.DetailEffect
            });

            actionMessages.Add($"{player.Username} rút thẻ Cơ Hội: {card.Name}.");

            switch (card.EffectCode)
            {
                case "FINE":
                    const long fine = 100000;
                    ChargePlayerForDebtUnsafe(
                        gameState,
                        player,
                        fine,
                        -1,
                        "tiền phạt thẻ Cơ Hội",
                        actionMessages);
                    break;

                case "JACKPOT":
                    const long jackpot = 500000;
                    player.Money += jackpot;
                    actionMessages.Add($"Nhận thưởng {jackpot:N0}.");
                    break;

                case "GO_TO_JAIL":
                    player.Position = 24;
                    SendPlayerToIslandUnsafe(player);
                    actionMessages.Add("Đi tới Đảo Hoang.");
                    break;

                case "SKIP_TURN":
                    player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                    player.SkipReason = "CARD_SKIP";
                    actionMessages.Add("Bị đóng băng giao dịch, bỏ lượt kế tiếp.");
                    break;

                case "TAX_PENALTY":
                    long penalty = (player.Money / 10 / 1000) * 1000;
                    ChargePlayerForDebtUnsafe(
                        gameState,
                        player,
                        penalty,
                        -1,
                        "phạt thuế thẻ Cơ Hội",
                        actionMessages);
                    break;

                case "CHARITY_PAY":
                    ApplyCharityPayUnsafe(gameState, player, actionMessages);
                    break;

                case "GO_TO_WORLD_TOUR":
                    player.Position = 8;
                    player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                    player.SkipReason = "WORLD_TOUR";
                    actionMessages.Add("Bay đến Du Lịch Thế Giới và chờ cất cánh lượt sau.");
                    break;

                case "FREE_RENT":
                    player.HasFreeRentCard = true;
                    actionMessages.Add("Nhận Khiên Miễn Trừ. Dùng thẻ để kích hoạt miễn tiền thuê lần sau.");
                    break;

                case "FLIGHT":
                    player.HasFlightCard = true;
                    actionMessages.Add("Nhận thẻ Bay, có thể dùng ở lượt sau.");
                    break;

                case "ESCAPE_ISLAND":
                    player.HasEscapeIslandCard = true;
                    actionMessages.Add("Nhận thẻ thoát Đảo Hoang.");
                    break;

                case "FREE_UPGRADE":
                    player.HasFreeUpgradeCard = true;
                    actionMessages.Add("Nhận thẻ nâng cấp miễn phí.");
                    break;

                case "FORCE_DOUBLE":
                    player.HasForceDoubleCard = true;
                    actionMessages.Add("Nhận thẻ Xúc Xắc Ma Thuật.");
                    break;

                case "EARTHQUAKE":
                    player.HasEarthquakeCard = true;
                    actionMessages.Add("Nhận thẻ Động Đất. Dùng thẻ để chọn thành phố đối thủ cần phá hủy.");
                    break;

                case "POWER_OUTAGE":
                    player.HasPowerOutageCard = true;
                    actionMessages.Add("Nhận thẻ Cúp Điện. Dùng thẻ để chọn đất đối thủ bị vô hiệu tiền thuê.");
                    break;

                case "MOVE_CHAMPIONSHIP":
                    player.HasMoveChampionshipCard = true;
                    actionMessages.Add("Nhận thẻ Đăng Cai Giải Đấu. Dùng thẻ để chọn thành phố của bạn.");
                    break;

                default:
                    actionMessages.Add($"Hiệu ứng {card.EffectCode} chưa được hỗ trợ.");
                    break;
            }
        }
        public static void ApplyCharityPayUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            long totalPaid = 0;

            foreach (GamePlayerState other in gameState.Players)
            {
                if (other == null ||
                    other.PlayerIndex == player.PlayerIndex ||
                    other.IsBankrupt ||
                    player.Money <= 0)
                {
                    continue;
                }

                long amount = Math.Min(50000, player.Money);
                player.Money -= amount;
                other.Money += amount;
                totalPaid += amount;
            }

            actionMessages.Add($"{player.Username} từ thiện {totalPaid:N0} cho các đối thủ.");
        }
        public static void ApplyEarthquakeAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.Type == "City" && p.OwnerPlayerIndex >= 0 &&
                    p.OwnerPlayerIndex != player.PlayerIndex &&
                    (p.HasHotel || p.HouseCount > 0))
                .OrderByDescending(p => p.HasHotel ? 4 : p.HouseCount)
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có thành phố đối thủ đủ điều kiện để Động Đất phá hủy.");
                return;
            }

            if (target.HasHotel)
            {
                target.HasHotel = false;
                target.HouseCount = 3;
            }
            else
            {
                target.HouseCount = Math.Max(0, target.HouseCount - 1);
            }

            actionMessages.Add($"Động Đất làm {target.Name} giảm 1 cấp.");
        }
        public static void ApplyPowerOutageAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex >= 0 && p.OwnerPlayerIndex != player.PlayerIndex &&
                    (p.Type == "City" || p.Type == "Resort"))
                .OrderByDescending(p => GetCurrentRentUnsafe(gameState, p))
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có đất đối thủ đủ điều kiện để cúp điện.");
                return;
            }

            target.PowerOutageTurn = gameState.TurnNumber + 2;
            actionMessages.Add($"{target.Name} bị cúp điện trong 2 lượt.");
        }
        public static void ApplyMoveChampionshipAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.Type == "City" && p.OwnerPlayerIndex == player.PlayerIndex)
                .OrderByDescending(p => GetCurrentRentUnsafe(gameState, p))
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có thành phố thuộc sở hữu để dời Giải Vô Địch.");
                return;
            }

            gameState.WorldChampionshipPosition = target.PositionIndex;
            actionMessages.Add($"Dời Giải Vô Địch về {target.Name}.");
        }
        public static string NormalizeCardEffectCode(string cardIdOrEffectCode)
        {
            if (string.IsNullOrWhiteSpace(cardIdOrEffectCode))
                return "";

            string value = cardIdOrEffectCode.Trim();

            if (CardDatabase.Cards.TryGetValue(value, out ChanceCardConfig card))
                return card.EffectCode ?? "";

            return value.ToUpperInvariant();
        }
        public static bool RequiresCardTarget(string effectCode)
        {
            switch (NormalizeCardEffectCode(effectCode))
            {
                case "FLIGHT":
                case "FREE_UPGRADE":
                case "EARTHQUAKE":
                case "POWER_OUTAGE":
                case "MOVE_CHAMPIONSHIP":
                case "WORLD_TOUR":
                case "WORLD_CHAMPIONSHIP_HOST":
                    return true;
                default:
                    return false;
            }
        }
        public static bool PlayerHasHeldCardUnsafe(GamePlayerState player, string effectCode)
        {
            if (player == null)
                return false;

            switch (NormalizeCardEffectCode(effectCode))
            {
                case "FREE_RENT":
                    return player.HasFreeRentCard;
                case "ESCAPE_ISLAND":
                    return player.HasEscapeIslandCard;
                case "FLIGHT":
                    return player.HasFlightCard;
                case "FREE_UPGRADE":
                    return player.HasFreeUpgradeCard;
                case "FORCE_DOUBLE":
                    return player.HasForceDoubleCard;
                case "EARTHQUAKE":
                    return player.HasEarthquakeCard;
                case "POWER_OUTAGE":
                    return player.HasPowerOutageCard;
                case "MOVE_CHAMPIONSHIP":
                    return player.HasMoveChampionshipCard;
                case "WORLD_TOUR":
                case "WORLD_CHAMPIONSHIP_HOST":
                    return true;
                default:
                    return false;
            }
        }
        public static List<int> BuildCardTargetPositionsUnsafe(
                GameState gameState,
                GamePlayerState player,
                string effectCode)
        {
            List<int> targets = new List<int>();

            if (gameState == null || player == null)
                return targets;

            switch (NormalizeCardEffectCode(effectCode))
            {
                case "FLIGHT":
                    targets.AddRange(BoardDatabase.Squares.Keys.OrderBy(p => p));
                    break;
                case "FREE_UPGRADE":
                    targets.AddRange(gameState.Properties.Values
                        .Where(p => p.Type == "City" &&
                                    p.OwnerPlayerIndex == player.PlayerIndex &&
                                    !p.HasHotel)
                        .OrderBy(p => p.PositionIndex)
                        .Select(p => p.PositionIndex));
                    break;
                case "EARTHQUAKE":
                    targets.AddRange(gameState.Properties.Values
                        .Where(p => p.Type == "City" &&
                                    p.OwnerPlayerIndex >= 0 &&
                                    p.OwnerPlayerIndex != player.PlayerIndex &&
                                    (p.HasHotel || p.HouseCount > 0))
                        .OrderBy(p => p.PositionIndex)
                        .Select(p => p.PositionIndex));
                    break;
                case "POWER_OUTAGE":
                    targets.AddRange(gameState.Properties.Values
                        .Where(p => p.OwnerPlayerIndex >= 0 &&
                                    p.OwnerPlayerIndex != player.PlayerIndex &&
                                    (p.Type == "City" || p.Type == "Resort"))
                        .OrderBy(p => p.PositionIndex)
                        .Select(p => p.PositionIndex));
                    break;
                case "MOVE_CHAMPIONSHIP":
                case "WORLD_CHAMPIONSHIP_HOST":
                    targets.AddRange(gameState.Properties.Values
                        .Where(p => p.Type == "City" &&
                                    p.OwnerPlayerIndex == player.PlayerIndex)
                        .OrderBy(p => p.PositionIndex)
                        .Select(p => p.PositionIndex));
                    break;

                case "WORLD_TOUR":
                    targets.AddRange(BoardDatabase.Squares.Keys.OrderBy(p => p));
                    break;
            }

            return targets;
        }
        public static bool TryApplyHeldCardEffectUnsafe(
                GameState gameState,
                GamePlayerState player,
                string effectCode,
                int? targetPosition,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents,
                out string failMessage)
        {
            failMessage = "";
            effectCode = NormalizeCardEffectCode(effectCode);

            if (gameState == null || player == null)
            {
                failMessage = "Không tìm thấy trạng thái game hoặc người chơi.";
                return false;
            }

            if (!PlayerHasHeldCardUnsafe(player, effectCode))
            {
                failMessage = "Bạn không sở hữu thẻ này hoặc thẻ đã được dùng.";
                return false;
            }

            if (RequiresCardTarget(effectCode))
            {
                if (!targetPosition.HasValue)
                {
                    failMessage = "Thẻ này cần chọn mục tiêu.";
                    return false;
                }

                List<int> validTargets = BuildCardTargetPositionsUnsafe(gameState, player, effectCode);

                if (!validTargets.Contains(targetPosition.Value))
                {
                    failMessage = "Mục tiêu không hợp lệ cho thẻ này.";
                    return false;
                }
            }

            switch (effectCode)
            {
                case "FREE_RENT":
                    player.HasFreeRentCard = false;
                    player.IsFreeRentShieldActive = true;
                    actionMessages.Add($"{player.Username} kích hoạt Khiên Miễn Trừ cho lần trả tiền thuế tiếp theo.");
                    return true;

                case "ESCAPE_ISLAND":
                    if (!player.IsOnIsland && player.JailTurnsLeft <= 0)
                    {
                        failMessage = "Chỉ có thể dùng để thoát Đảo Hoang khi đang ở Đảo Hoang.";
                        return false;
                    }

                    player.HasEscapeIslandCard = false;
                    player.IsOnIsland = false;
                    player.JailTurnsLeft = 0;
                    player.SkipTurnsLeft = 0;
                    player.SkipReason = "";
                    actionMessages.Add($"{player.Username} dùng Trực Thăng Cứu Hộ và thoát Đảo Hoang.");
                    return true;

                case "FORCE_DOUBLE":
                    if (gameState.HasRolledThisTurn)
                    {
                        failMessage = "Chỉ có thể dùng Xúc Xắc Ma Thuật trước khi đổ xúc xắc.";
                        return false;
                    }

                    if (!player.IsOnIsland && player.SkipTurnsLeft > 0)
                    {
                        failMessage = "Không thể dùng Xúc Xắc Ma Thuật khi đang bị mất lượt.";
                        return false;
                    }

                    player.HasForceDoubleCard = false;
                    gameState.ForceDoubleThisTurn = true;
                    actionMessages.Add($"{player.Username} kích hoạt Xúc Xắc Ma Thuật cho lần roll này.");
                    return true;

                case "FLIGHT":
                    return ApplyFlightCardUnsafe(gameState, player, targetPosition ?? 0, actionMessages, cardDrawEvents, out failMessage);

                case "FREE_UPGRADE":
                    return ApplyFreeUpgradeCardUnsafe(gameState, player, targetPosition ?? 0, actionMessages, out failMessage);

                case "EARTHQUAKE":
                    return ApplyEarthquakeCardUnsafe(gameState, player, targetPosition ?? 0, actionMessages, out failMessage);

                case "POWER_OUTAGE":
                    return ApplyPowerOutageCardUnsafe(gameState, player, targetPosition ?? 0, actionMessages, out failMessage);

                case "MOVE_CHAMPIONSHIP":
                    return ApplyMoveChampionshipCardUnsafe(gameState, player, targetPosition ?? 0, actionMessages, out failMessage);

                case "WORLD_TOUR":
                    return ApplyWorldTourChoiceUnsafe(gameState, player, targetPosition ?? 0, actionMessages, cardDrawEvents, out failMessage);

                case "WORLD_CHAMPIONSHIP_HOST":
                    return ApplyChampionshipHostChoiceUnsafe(gameState, player, targetPosition ?? 0, actionMessages, out failMessage);

                default:
                    failMessage = $"The {effectCode} chưa được hỗ trợ trong handle.";
                    return false;
            }
        }
        private static bool ApplyFlightCardUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents,
                out string failMessage)
        {
            failMessage = "";

            if (!BoardDatabase.Squares.ContainsKey(targetPosition))
            {
                failMessage = "Ô đích không hợp lệ.";
                return false;
            }

            int oldPosition = player.Position;
            int boardSize = BoardDatabase.Squares.Count;
            player.HasFlightCard = false;
            player.Position = targetPosition;

            if (targetPosition < oldPosition)
            {
                const long startBonus = 200000;
                player.Money += startBonus;
                actionMessages.Add($"{player.Username} bay qua Bắt Đầu và nhận {startBonus:N0}.");
            }

            actionMessages.Add($"{player.Username} dùng Vé Máy Bay từ ô {oldPosition} đến ô {targetPosition}.");
            ApplyRentOnLandingUnsafe(gameState, player, targetPosition, actionMessages);
            ApplySpecialSquareEffectUnsafe(gameState, player, targetPosition, actionMessages, cardDrawEvents);

            gameState.LastDice1 = 0;
            gameState.LastDice2 = 0;
            gameState.LastDiceTotal = 0;
            gameState.LastMovedPlayerIndex = player.PlayerIndex;
            gameState.LastMoveFromPosition = oldPosition;
            gameState.LastMoveToPosition = targetPosition;
            gameState.LastFinalPosition = player.Position;
            return true;
        }
        private static bool ApplyFreeUpgradeCardUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (!gameState.Properties.TryGetValue(targetPosition, out GamePropertyState property))
            {
                failMessage = "Không tìm thấy ô đất cần nâng cấp.";
                return false;
            }

            if (property.Type != "City" ||
                property.OwnerPlayerIndex != player.PlayerIndex ||
                property.HasHotel)
            {
                failMessage = "Ô đất này không thể nâng cấp miễn phí.";
                return false;
            }

            player.HasFreeUpgradeCard = false;

            if (property.HouseCount >= 3)
            {
                property.HouseCount = 3;
                property.HasHotel = true;
            }
            else
            {
                property.HouseCount++;
            }

            actionMessages.Add($"{player.Username} dùng Giấy Phép Xây Dựng nâng cấp {property.Name} lên {DescribePropertyLevelUnsafe(property)} miễn phí.");
            return true;
        }
        private static bool ApplyEarthquakeCardUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (!gameState.Properties.TryGetValue(targetPosition, out GamePropertyState property) ||
                property.Type != "City" ||
                property.OwnerPlayerIndex < 0 ||
                property.OwnerPlayerIndex == player.PlayerIndex ||
                (!property.HasHotel && property.HouseCount <= 0))
            {
                failMessage = "Mục tiêu Động Đất không hợp lệ.";
                return false;
            }

            player.HasEarthquakeCard = false;

            if (property.HasHotel)
            {
                property.HasHotel = false;
                property.HouseCount = 3;
            }
            else
            {
                property.HouseCount = Math.Max(0, property.HouseCount - 1);
            }

            actionMessages.Add($"{player.Username} dùng Động Đất làm {property.Name} giảm 1 cấp.");
            return true;
        }
        private static bool ApplyPowerOutageCardUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (!gameState.Properties.TryGetValue(targetPosition, out GamePropertyState property) ||
                property.OwnerPlayerIndex < 0 ||
                property.OwnerPlayerIndex == player.PlayerIndex ||
                (property.Type != "City" && property.Type != "Resort"))
            {
                failMessage = "Mục tiêu Cúp Điện không hợp lệ.";
                return false;
            }

            player.HasPowerOutageCard = false;
            property.PowerOutageTurn = gameState.TurnNumber + 2;
            actionMessages.Add($"{player.Username} dùng Cúp Điện làm {property.Name} mất điện trong 2 lượt.");
            return true;
        }
        private static bool ApplyMoveChampionshipCardUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (!gameState.Properties.TryGetValue(targetPosition, out GamePropertyState property) ||
                property.Type != "City" ||
                property.OwnerPlayerIndex != player.PlayerIndex)
            {
                failMessage = "Mục tiêu đăng cai giải đấu không hợp lệ.";
                return false;
            }

            player.HasMoveChampionshipCard = false;
            gameState.WorldChampionshipPosition = targetPosition;
            actionMessages.Add($"{player.Username} đổi Giải Vô Địch về {property.Name}.");
            return true;
        }

        private static bool ApplyWorldTourChoiceUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                List<CardDrawEvent> cardDrawEvents,
                out string failMessage)
        {
            failMessage = "";

            if (!BoardDatabase.Squares.ContainsKey(targetPosition))
            {
                failMessage = "Ô đích không hợp lệ.";
                return false;
            }

            int oldPosition = player.Position;
            player.Position = targetPosition;
            player.SkipReason = ""; // Clear WorldTour skip reason

            if (targetPosition < oldPosition)
            {
                const long startBonus = 200000;
                player.Money += startBonus;
                actionMessages.Add($"{player.Username} bay qua Bắt Đầu và nhận {startBonus:N0}.");
            }

            actionMessages.Add($"{player.Username} bay từ ô {oldPosition} đến ô {targetPosition} bằng Du Lịch Thế Giới.");
            ApplyRentOnLandingUnsafe(gameState, player, targetPosition, actionMessages);
            ApplySpecialSquareEffectUnsafe(gameState, player, targetPosition, actionMessages, cardDrawEvents);

            // Update movement state so visuals and logic know where they landed
            // LastDiceTotal must be > 0 for client to animate the move
            gameState.LastDice1 = 0;
            gameState.LastDice2 = 0;
            gameState.LastDiceTotal = 1;
            gameState.LastMovedPlayerIndex = player.PlayerIndex;
            gameState.LastMoveFromPosition = oldPosition;
            gameState.LastMoveToPosition = targetPosition;
            gameState.LastFinalPosition = player.Position;
            
            return true;
        }

        private static bool ApplyChampionshipHostChoiceUnsafe(
                GameState gameState,
                GamePlayerState player,
                int targetPosition,
                List<string> actionMessages,
                out string failMessage)
        {
            failMessage = "";

            if (!gameState.Properties.TryGetValue(targetPosition, out GamePropertyState property) ||
                property.Type != "City" ||
                property.OwnerPlayerIndex != player.PlayerIndex)
            {
                failMessage = "Mục tiêu đăng cai giải đấu không hợp lệ.";
                return false;
            }

            gameState.WorldChampionshipPosition = targetPosition;
            actionMessages.Add($"{player.Username} đăng cai Giải Vô Địch tại {property.Name}.");
            return true;
        }
        public static void ClearPendingCardChoiceUnsafe(GameState gameState)
        {
            if (gameState == null)
                return;

            gameState.IsWaitingForCardChoice = false;
            gameState.PendingCardEffectCode = "";
            gameState.PendingCardPlayerUsername = "";
            gameState.PendingCardTargetPositions.Clear();
        }
        public static void AddGameLogUnsafe(GameState gameState, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            gameState.ActionLog.Add($"Turn {gameState.TurnNumber}: {message}");

            while (gameState.ActionLog.Count > 12)
            {
                gameState.ActionLog.RemoveAt(0);
            }
        }
        public static long GetCurrentRentUnsafe(GameState gameState, GamePropertyState property)
        {
            if (property == null || property.RentPrices == null || property.RentPrices.Count == 0)
            {
                return 0;
            }

            int rentIndex = property.HasHotel
                ? property.RentPrices.Count - 1
                : Math.Max(0, Math.Min(property.HouseCount, property.RentPrices.Count - 1));

            long baseRent = property.RentPrices[rentIndex];
            int bonusMultiplier = 0;

            if (gameState != null && property.OwnerPlayerIndex >= 0 && property.Type == "City" && !string.IsNullOrEmpty(property.ColorSet))
            {
                var colorGroup = gameState.Properties.Values
                    .Where(p => p.Type == "City" && p.ColorSet == property.ColorSet)
                    .ToList();
                if (colorGroup.Count > 0 && colorGroup.All(p => p.OwnerPlayerIndex == property.OwnerPlayerIndex))
                {
                    bonusMultiplier = 1;
                }
            }

            return baseRent * Math.Max(1, property.Multiplier + bonusMultiplier);
        }
        public static long GetBuildCostUnsafe(GamePropertyState property)
        {
            if (property == null || property.Type != "City" || property.BuyPrice <= 0 || property.HasHotel)
            {
                return 0;
            }

            return property.HouseCount >= 3 ? property.BuyPrice : Math.Max(1, property.BuyPrice / 2);
        }
        public static string DescribePropertyLevelUnsafe(GamePropertyState property)
        {
            if (property == null)
            {
                return "không xác định";
            }

            if (property.HasHotel)
            {
                return "khách sạn";
            }

            if (property.HouseCount > 0)
            {
                return $"{property.HouseCount} nhà";
            }

            return "đất trống";
        }
        public static void ResolveBankruptcyAndWinnerUnsafe(
                GameState gameState,
                GamePlayerState currentPlayer,
                List<string> actionMessages)
        {
            foreach (GamePlayerState player in gameState.Players.OrderBy(p => p.PlayerIndex))
            {
                if (player.Money < 0 && !player.IsBankrupt)
                {
                    HandleBankruptcyUnsafe(gameState, player, actionMessages);
                }
            }

            List<GamePlayerState> activePlayers = gameState.Players
                .Where(p => !p.IsBankrupt && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            // Check for Monopoly Victory Conditions (Fast Win)
            GamePlayerState monopolyWinner = null;
            string victoryReason = "";

            foreach (GamePlayerState player in activePlayers)
            {
                // 1. Resort Monopoly: Owning at least 4 resorts (out of 5 total in this board)
                int ownedResortsCount = gameState.Properties.Values.Count(p => p.Type == "Resort" && p.OwnerPlayerIndex == player.PlayerIndex);
                if (ownedResortsCount >= 4)
                {
                    monopolyWinner = player;
                    victoryReason = "Resort Monopoly (Sở hữu trọn bộ 4 khu nghỉ dưỡng)";
                    break;
                }

                // 2. Line Monopoly: Owning all cities on at least one edge (line) of the board
                bool hasLineMonopoly = false;
                for (int line = 1; line <= 4; line++)
                {
                    string lineStr = line.ToString();
                    var lineCities = gameState.Properties.Values.Where(p => p.LineIndex == lineStr && p.Type == "City").ToList();
                    if (lineCities.Count > 0 && lineCities.All(p => p.OwnerPlayerIndex == player.PlayerIndex))
                    {
                        hasLineMonopoly = true;
                        break;
                    }
                }
                if (hasLineMonopoly)
                {
                    monopolyWinner = player;
                    victoryReason = "Line Monopoly (Sở hữu toàn bộ thành phố trên 1 cạnh bàn cờ)";
                    break;
                }

                // 3. Triple Monopoly: Owning all cities of at least 3 color sets
                int colorMonopoliesCount = 0;
                var colorGroups = gameState.Properties.Values
                    .Where(p => p.Type == "City" && !string.IsNullOrEmpty(p.ColorSet))
                    .GroupBy(p => p.ColorSet);
                foreach (var group in colorGroups)
                {
                    if (group.All(p => p.OwnerPlayerIndex == player.PlayerIndex))
                    {
                        colorMonopoliesCount++;
                    }
                }
                if (colorMonopoliesCount >= 3)
                {
                    monopolyWinner = player;
                    victoryReason = "Triple Monopoly (Sở hữu 3 nhóm màu độc quyền)";
                    break;
                }
            }

            if (monopolyWinner != null)
            {
                gameState.IsFinished = true;
                gameState.WinnerUsername = monopolyWinner.Username;
                gameState.EndReason = victoryReason;
                gameState.HasRolledThisTurn = true;
                gameState.TurnEndsAtUtcTicks = 0;
                actionMessages.Add($"{monopolyWinner.Username} đạt chiến thắng nhanh ({victoryReason})!");
                actionMessages.Add($"Xếp hạng: {BuildRankingSummaryUnsafe(gameState)}");

                Console.WriteLine($"[GAME_OVER] Winner={gameState.WinnerUsername}, Reason={victoryReason}");
                return;
            }

            List<GamePlayerState> activeHumanPlayers = activePlayers
                .Where(p => !p.IsBot)
                .ToList();

            if (activePlayers.Count <= 1 || activeHumanPlayers.Count == 0)
            {
                gameState.IsFinished = true;

                if (activePlayers.Count > 0)
                {
                    var winner = activePlayers.OrderByDescending(p => p.Money).First();
                    gameState.WinnerUsername = winner.Username;
                    if (string.IsNullOrWhiteSpace(gameState.EndReason))
                        gameState.EndReason = "Phá sản — người chơi cuối cùng trụ lại";
                }
                else
                {
                    gameState.WinnerUsername = "Không có ai";
                    if (string.IsNullOrWhiteSpace(gameState.EndReason))
                        gameState.EndReason = "Tất cả người chơi đã rời/phá sản";
                }

                gameState.HasRolledThisTurn = true;
                gameState.TurnEndsAtUtcTicks = 0;
                actionMessages.Add($"{gameState.WinnerUsername} thắng trận.");
                actionMessages.Add($"Xếp hạng: {BuildRankingSummaryUnsafe(gameState)}");

                Console.WriteLine($"[GAME_OVER] Winner={gameState.WinnerUsername}");
                return;
            }

            if (currentPlayer != null && currentPlayer.IsBankrupt && !gameState.IsFinished)
            {
                GamePlayerState nextPlayer = GetNextTurnPlayerUnsafe(gameState);

                gameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                gameState.CurrentTurnUsername = nextPlayer.Username;
                gameState.TurnNumber++;
                gameState.HasRolledThisTurn = false;
                ResetTurnTimerUnsafe(gameState);

                actionMessages.Add($"Đến lượt {nextPlayer.Username}.");

                Console.WriteLine(
                    $"[AUTO_END_TURN] Bankrupt={currentPlayer.Username}, " +
                    $"Next={nextPlayer.Username}, Turn={gameState.TurnNumber}"
                );
            }
        }
        public static void HandleBankruptcyUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            player.IsBankrupt = true;
            player.BankruptcyOrder = GetNextBankruptcyOrderUnsafe(gameState);
            player.Money = 0;
            player.HasFreeRentCard = false;
            player.IsFreeRentShieldActive = false;
            player.HasEscapeIslandCard = false;
            player.HasFlightCard = false;
            player.HasFreeUpgradeCard = false;
            player.HasForceDoubleCard = false;
            player.HasEarthquakeCard = false;
            player.HasPowerOutageCard = false;
            player.HasMoveChampionshipCard = false;
            player.IsOnIsland = false;
            player.JailTurnsLeft = 0;
            player.SkipTurnsLeft = 0;
            player.SkipReason = "";

            if (gameState.IsWaitingForPropertySale && gameState.PendingSalePlayerIndex == player.PlayerIndex)
                ClearPendingPropertySaleUnsafe(gameState);

            int releasedProperties = ReleasePlayerPropertiesUnsafe(gameState, player.PlayerIndex);
            actionMessages.Add($"{player.Username} đã phá sản và mất {releasedProperties} ô đất.");

            Console.WriteLine(
                $"[BANKRUPT] Player={player.Username}, Order={player.BankruptcyOrder}, ReleasedProperties={releasedProperties}"
            );
        }
        public static int GetNextBankruptcyOrderUnsafe(GameState gameState)
        {
            int maxOrder = 0;

            foreach (GamePlayerState player in gameState.Players)
            {
                if (player.BankruptcyOrder > maxOrder)
                    maxOrder = player.BankruptcyOrder;
            }

            return maxOrder + 1;
        }
        public static int ReleasePlayerPropertiesUnsafe(GameState gameState, int ownerPlayerIndex)
        {
            int releasedCount = 0;

            foreach (GamePropertyState property in gameState.Properties.Values)
            {
                if (property.OwnerPlayerIndex != ownerPlayerIndex)
                    continue;

                property.OwnerPlayerIndex = -1;
                property.HouseCount = 0;
                property.HasHotel = false;
                property.Multiplier = 1;
                property.PowerOutageTurn = 0;
                releasedCount++;
            }

            return releasedCount;
        }
        public static string BuildRankingSummaryUnsafe(GameState gameState)
        {
            List<GamePlayerState> rankedPlayers = GetRankedHumanPlayersUnsafe(gameState);

            List<string> parts = new List<string>();

            for (int i = 0; i < rankedPlayers.Count; i++)
            {
                parts.Add($"#{i + 1} {rankedPlayers[i].Username}");
            }

            return string.Join(", ", parts);
        }
        public static List<GameOverRankingResult> BuildGameOverRankingsUnsafe(GameState gameState)
        {
            List<GamePlayerState> rankedPlayers = GetRankedHumanPlayersUnsafe(gameState);

            List<GameOverRankingResult> rankings = new List<GameOverRankingResult>();

            for (int i = 0; i < rankedPlayers.Count; i++)
            {
                GamePlayerState player = rankedPlayers[i];
                ClientConnection playerConnection = ServerState.Clients.Values
                    .FirstOrDefault(c => c.Username == player.Username);

                rankings.Add(new GameOverRankingResult
                {
                    UserId = string.IsNullOrWhiteSpace(playerConnection?.Uid)
                        ? player.Username
                        : playerConnection.Uid,
                    DisplayName = player.Username,
                    Rank = i + 1,
                    ScoreEarned = GetScoreForRank(i + 1),
                    IdToken = playerConnection?.IdToken ?? ""
                });
            }

            return rankings;
        }

        private static List<GamePlayerState> GetRankedHumanPlayersUnsafe(GameState gameState)
        {
            IEnumerable<GamePlayerState> players = gameState.Players.Where(p => !p.IsBot);

            if (gameState.EndReason == "TIMEOUT")
            {
                return players
                    .OrderByDescending(p => GetPlayerNetWorthUnsafe(gameState, p))
                    .ThenByDescending(p => p.Money)
                    .ThenBy(p => p.IsBankrupt ? 1 : 0)
                    .ThenBy(p => p.PlayerIndex)
                    .ToList();
            }

            if (gameState.EndReason != null && gameState.EndReason.Contains("Monopoly"))
            {
                return players
                    .OrderBy(p => p.Username == gameState.WinnerUsername ? 0 : 1)
                    .ThenBy(p => p.IsBankrupt ? 1 : 0)
                    .ThenByDescending(p => p.IsBankrupt ? p.BankruptcyOrder : int.MaxValue)
                    .ThenByDescending(p => GetPlayerNetWorthUnsafe(gameState, p))
                    .ThenBy(p => p.PlayerIndex)
                    .ToList();
            }

            return players
                .OrderBy(p => p.IsBankrupt ? 1 : 0)
                .ThenByDescending(p => p.IsBankrupt ? p.BankruptcyOrder : int.MaxValue)
                .ThenBy(p => p.PlayerIndex)
                .ToList();
        }
        public static GamePlayerState GetNextTurnPlayerUnsafe(GameState gameState)
        {
            List<GamePlayerState> activePlayers = gameState.Players
                .Where(p => !p.IsBankrupt && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (activePlayers.Count == 0)
            {
                return gameState.Players.OrderBy(p => p.PlayerIndex).First();
            }

            GamePlayerState nextPlayer = activePlayers
                .FirstOrDefault(p => p.PlayerIndex > gameState.CurrentTurnPlayerIndex);

            return nextPlayer ?? activePlayers[0];
        }
        public static string GenerateRoomIdUnsafe()
        {
            string roomId;

            do
            {
                roomId = ServerState.Random.Next(1000, 9999).ToString();
            }
            while (ServerState.Rooms.ContainsKey(roomId));

            return roomId;
        }
        public static GameState CreateInitialGameState(Room room)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            int matchDurationSeconds = Math.Max(600, room.MatchDurationMinutes * 60);
            GameState gameState = new GameState
            {
                RoomId = room.RoomId,
                MapName = room.MapName,
                TurnNumber = 1,
                CurrentTurnPlayerIndex = room.Players[0].PlayerIndex,
                CurrentTurnUsername = room.Players[0].Username,
                HasRolledThisTurn = false,
                LastMovedPlayerIndex = -1,
                LastMoveFromPosition = -1,
                LastMoveToPosition = -1,
                LastFinalPosition = -1,
                TurnDurationSeconds = 45,
                IsFinished = false,
                WinnerUsername = "",
                MatchId = Guid.NewGuid().ToString("N"),
                GameOverBroadcasted = false,
                MatchDurationSeconds = matchDurationSeconds,
                MatchStartedAtUtcTicks = nowTicks,
                MatchEndsAtUtcTicks = DateTime.UtcNow.AddSeconds(matchDurationSeconds).Ticks,
                EndReason = "",
                IsPaused = false,
                PauseRequestedBy = "",
                PauseStartedAtUtcTicks = 0,
                PauseVotes = new List<string>(),
                WorldChampionshipPosition = 16,
                IsWaitingForCardChoice = false,
                PendingCardEffectCode = "",
                PendingCardPlayerUsername = "",
                PendingCardTargetPositions = new List<int>(),
                ForceDoubleThisTurn = false,
                IsWaitingForPropertySale = false,
                PendingSalePlayerIndex = -1,
                PendingSalePlayerUsername = "",
                PendingDebtAmount = 0,
                PendingDebtCreditorPlayerIndex = -1,
                PendingDebtReason = "",
                PendingSalePropertyPositions = new List<int>()
            };

            ResetTurnTimerUnsafe(gameState);

            foreach (RoomPlayer player in room.Players.OrderBy(p => p.PlayerIndex))
            {
                gameState.Players.Add(new GamePlayerState
                {
                    Username = player.Username,
                    IsBot = player.IsBot,
                    PlayerIndex = player.PlayerIndex,
                    Position = 0,
                    Money = 500000,
                    IsBankrupt = false,
                    BankruptcyOrder = 0,
                    IsConnected = true,
                    ConsecutiveDoubles = 0,
                    JailTurnsLeft = 0,
                    HasFreeRentCard = false,
                    IsFreeRentShieldActive = false,
                    HasEscapeIslandCard = false,
                    HasFlightCard = false,
                    HasFreeUpgradeCard = false,
                    HasForceDoubleCard = false,
                    HasEarthquakeCard = false,
                    HasPowerOutageCard = false,
                    HasMoveChampionshipCard = false,
                    IsOnIsland = false,
                    SkipTurnsLeft = 0,
                    SkipReason = "",
                    LastDrawnCardId = ""
                });
            }

            foreach (var kvp in BoardDatabase.Squares)
            {
                var square = kvp.Value;

                gameState.Properties[square.PositionIndex] = new GamePropertyState
                {
                    PositionIndex = square.PositionIndex,
                    Name = square.Name,
                    Type = square.Type,
                    ColorSet = square.ColorSet,
                    LineIndex = square.LineIndex,
                    BuyPrice = square.BuyPrice,
                    RentPrices = new List<long>(square.RentPrices),
                    OwnerPlayerIndex = -1,
                    HouseCount = 0,
                    HasHotel = false,
                    Multiplier = 1,
                    PowerOutageTurn = 0
                };
            }

            return gameState;
        }

        public static long GetPlayerNetWorthUnsafe(GameState gameState, GamePlayerState player)
        {
            if (gameState == null || player == null)
                return 0;

            long netWorth = Math.Max(0, player.Money);
            foreach (GamePropertyState property in gameState.Properties.Values)
            {
                if (property.OwnerPlayerIndex == player.PlayerIndex)
                    netWorth += GetPropertySaleValueUnsafe(property);
            }

            return netWorth;
        }

        public static void FinishMatchByTimeUnsafe(GameState gameState, List<string> actionMessages)
        {
            if (gameState == null || gameState.IsFinished)
                return;

            List<GamePlayerState> ranked = gameState.Players
                .Where(p => !p.IsBot)
                .OrderByDescending(p => GetPlayerNetWorthUnsafe(gameState, p))
                .ThenByDescending(p => p.Money)
                .ThenBy(p => p.IsBankrupt ? 1 : 0)
                .ThenBy(p => p.PlayerIndex)
                .ToList();

            gameState.IsFinished = true;
            gameState.EndReason = "TIMEOUT";
            gameState.WinnerUsername = ranked.Count > 0 ? ranked[0].Username : "Không có ai";
            gameState.HasRolledThisTurn = true;
            gameState.TurnEndsAtUtcTicks = 0;
            gameState.MatchEndsAtUtcTicks = DateTime.UtcNow.Ticks;
            gameState.IsBotPlaying = false;
            actionMessages.Add($"Hết thời gian trận. {gameState.WinnerUsername} dẫn đầu theo tổng tài sản.");
            actionMessages.Add($"Xếp hạng: {BuildRankingSummaryUnsafe(gameState)}");
        }
        public static int GetScoreForRank(int rank)
        {
            switch (rank)
            {
                case 1:
                    return 100;
                case 2:
                    return 50;
                case 3:
                    return 20;
                default:
                    return 5;
            }
        }
        public static async Task PersistMatchResultsAsync(string matchId, List<GameOverRankingResult> rankings)
        {
            foreach (GameOverRankingResult ranking in rankings)
            {
                if (string.IsNullOrWhiteSpace(ranking.UserId) || string.IsNullOrWhiteSpace(ranking.IdToken))
                {
                    Console.WriteLine($"[MATCH_RESULT_SKIP] Missing Firebase identity for {ranking.DisplayName}.");
                    continue;
                }

                bool success = await ServiceLocator.FirebaseApi.UpdatePlayerMatchResultAsync(
                    ranking.UserId,
                    ranking.IdToken,
                    ranking.DisplayName,
                    ranking.Rank,
                    ranking.ScoreEarned,
                    matchId
                );

                if (!success)
                {
                    Console.WriteLine($"[MATCH_RESULT_FAIL] User={ranking.DisplayName}, Rank={ranking.Rank}");
                }
            }
        }
    
        public static bool TryBuyPropertyUnsafe(GameState gameState, GamePlayerState player, GamePropertyState property, out string errorMessage)
        {
            if (property.Type != "City" && property.Type != "Resort") { errorMessage = $"Ô {property.Name} không thể mua."; return false; }
            if (property.OwnerPlayerIndex >= 0) { errorMessage = $"Ô {property.Name} đã có chủ."; return false; }
            if (property.BuyPrice <= 0) { errorMessage = $"Ô {property.Name} chưa có giá mua hợp lệ."; return false; }
            if (player.Money < property.BuyPrice) { errorMessage = $"Bạn không đủ tiền mua {property.Name}."; return false; }
            player.Money -= property.BuyPrice;
            property.OwnerPlayerIndex = player.PlayerIndex;
            errorMessage = "";
            return true;
        }

        public static bool TryBuildPropertyUnsafe(GameState gameState, GamePlayerState player, GamePropertyState property, out string errorMessage)
        {
            if (property.Type != "City") { errorMessage = $"Ô {property.Name} không thể xây nhà."; return false; }
            if (property.OwnerPlayerIndex != player.PlayerIndex) { errorMessage = $"Bạn không sở hữu {property.Name}."; return false; }
            if (property.HasHotel) { errorMessage = $"{property.Name} đã có khách sạn."; return false; }
            int firstRoundTurnLimit = Math.Max(1, gameState.Players.Count);
            if (gameState.TurnNumber <= firstRoundTurnLimit && property.HouseCount >= 1)
            {
                errorMessage = "Vòng đầu tiên chỉ được xây tối đa 1 nhà trên mỗi thành phố.";
                return false;
            }
            long buildCost = GetBuildCostUnsafe(property);
            if (buildCost <= 0) { errorMessage = $"Lỗi cấu hình giá xây dựng cho {property.Name}."; return false; }
            if (player.Money < buildCost) { errorMessage = $"Bạn không đủ tiền xây nhà tại {property.Name}."; return false; }
            player.Money -= buildCost;
            if (property.HouseCount < 3 && !property.HasHotel) { property.HouseCount++; }
            else if (property.HouseCount == 3 && !property.HasHotel) { property.HasHotel = true; }
            errorMessage = "";
            return true;
        }
    
        public static void StartNextTurnUnsafe(GameState gameState, out GamePlayerState? nextPlayer)
        {
            nextPlayer = GetNextTurnPlayerUnsafe(gameState);
            if (nextPlayer != null)
            {
                gameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                gameState.CurrentTurnUsername = nextPlayer.Username;
                gameState.HasRolledThisTurn = false;
                gameState.ForceDoubleThisTurn = false;
                gameState.TurnNumber++;
            }
        }
}
}
