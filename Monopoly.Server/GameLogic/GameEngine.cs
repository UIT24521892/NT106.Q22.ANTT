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
using Monopoly.Server.Models.State;
using Monopoly.Server.Services;
using Monopoly.Server.Network;


namespace Monopoly.Server.GameLogic
{
    public static class GameEngine
    {
        public static void ResetTurnTimerUnsafe(GameState gameState)
        {
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
                    player.Money -= taxAmount;
                    actionMessages.Add($"{player.Username} vào ô Thu? và n?p {taxAmount:N0}.");
                    break;

                case "Chance":
                    ApplyChanceEffectUnsafe(gameState, player, actionMessages, cardDrawEvents);
                    break;

                case "LostIsland":
                    SendPlayerToIslandUnsafe(player);
                    actionMessages.Add($"{player.Username} vào Ð?o Hoang và có 3 lu?t d? l?c dôi thoát ra.");
                    break;

                case "WorldChampionship":
                    ApplyWorldChampionshipUnsafe(gameState, player, actionMessages);
                    isChampionshipPosition = false;
                    break;

                case "WorldTour":
                    player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                    player.SkipReason = "WORLD_TOUR";
                    actionMessages.Add($"{player.Username} d?n Du L?ch Th? Gi?i và s? b? lu?t k? ti?p.");
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
                $"{player.Username} di t? ô {oldPosition} d?n ô {diceLandingPosition}."
            );

            player.Position = diceLandingPosition;

            if (rawPosition >= boardSize)
            {
                const long startBonus = 300000;
                player.Money += startBonus;
                actionMessages.Add($"{player.Username} di qua B?t Ð?u và nh?n {startBonus:N0}.");
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
                actionMessages.Add($"{landedProperty.Name} dang m?t di?n, mi?n ti?n thuê lu?t này.");
                return;
            }

            if (player.IsFreeRentShieldActive)
            {
                player.IsFreeRentShieldActive = false;
                actionMessages.Add($"{player.Username} dùng Khiên Mi?n Tr?, không ph?i tr? ti?n thuê {landedProperty.Name}.");
                return;
            }

            long rent = GetCurrentRentUnsafe(landedProperty);
            long paidAmount = Math.Min(rent, Math.Max(0, player.Money));
            player.Money -= rent;
            owner.Money += paidAmount;

            actionMessages.Add(
                $"{player.Username} tr? {paidAmount:N0}/{rent:N0} ti?n thuê {landedProperty.Name} cho {owner.Username}."
            );

            Console.WriteLine(
                $"[RENT] Room={gameState.RoomId}, From={player.Username}, To={owner.Username}, " +
                $"Property={landedProperty.Name}, Rent={rent}, Paid={paidAmount}, " +
                $"PayerMoney={player.Money}, OwnerMoney={owner.Money}"
            );
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
                actionMessages.Add($"{winner.Username} th?ng Gi?i Vô Ð?ch và thu {totalCollected:N0} t? các d?i th?.");
            }
            else
            {
                actionMessages.Add($"{winner.Username} d?n Gi?i Vô Ð?ch nhung không thu du?c ti?n.");
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
                actionMessages.Add($"{player.Username} rút th? Co H?i nhung không có d? li?u th?.");
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

            actionMessages.Add($"{player.Username} rút th? Co H?i: {card.Name}.");

            switch (card.EffectCode)
            {
                case "FINE":
                    const long fine = 100000;
                    player.Money -= fine;
                    actionMessages.Add($"B? ph?t {fine:N0}.");
                    break;

                case "JACKPOT":
                    const long jackpot = 500000;
                    player.Money += jackpot;
                    actionMessages.Add($"Nh?n thu?ng {jackpot:N0}.");
                    break;

                case "GO_TO_JAIL":
                    player.Position = 24;
                    SendPlayerToIslandUnsafe(player);
                    actionMessages.Add("Ði t?i Ð?o Hoang.");
                    break;

                case "SKIP_TURN":
                    player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                    player.SkipReason = "CARD_SKIP";
                    actionMessages.Add("B? dóng bang giao d?ch, b? lu?t k? ti?p.");
                    break;

                case "TAX_PENALTY":
                    long penalty = (player.Money / 10 / 1000) * 1000;
                    player.Money -= penalty;
                    actionMessages.Add($"N?p ph?t thu? {penalty:N0}.");
                    break;

                case "CHARITY_PAY":
                    ApplyCharityPayUnsafe(gameState, player, actionMessages);
                    break;

                case "GO_TO_WORLD_TOUR":
                    player.Position = 8;
                    player.SkipTurnsLeft = Math.Max(player.SkipTurnsLeft, 1);
                    player.SkipReason = "WORLD_TOUR";
                    actionMessages.Add("Bay d?n Du L?ch Th? Gi?i và ch? c?t cánh lu?t sau.");
                    break;

                case "FREE_RENT":
                    player.HasFreeRentCard = true;
                    actionMessages.Add("Nh?n Khiên Mi?n Tr?. Dùng th? d? kích ho?t mi?n ti?n thuê l?n sau.");
                    break;

                case "FLIGHT":
                    player.HasFlightCard = true;
                    actionMessages.Add("Nh?n th? Bay, có th? dùng ? lu?t sau.");
                    break;

                case "ESCAPE_ISLAND":
                    player.HasEscapeIslandCard = true;
                    actionMessages.Add("Nh?n th? thoát Ð?o Hoang.");
                    break;

                case "FREE_UPGRADE":
                    player.HasFreeUpgradeCard = true;
                    actionMessages.Add("Nh?n th? nâng c?p mi?n phí.");
                    break;

                case "FORCE_DOUBLE":
                    player.HasForceDoubleCard = true;
                    actionMessages.Add("Nh?n th? Xúc X?c Ma Thu?t.");
                    break;

                case "EARTHQUAKE":
                    player.HasEarthquakeCard = true;
                    actionMessages.Add("Nh?n th? Ð?ng Ð?t. Dùng th? d? ch?n thành ph? d?i th? c?n phá h?y.");
                    break;

                case "POWER_OUTAGE":
                    player.HasPowerOutageCard = true;
                    actionMessages.Add("Nh?n th? Cúp Ði?n. Dùng th? d? ch?n d?t d?i th? b? vô hi?u rent.");
                    break;

                case "MOVE_CHAMPIONSHIP":
                    player.HasMoveChampionshipCard = true;
                    actionMessages.Add("Nh?n th? Ðang Cai Gi?i Ð?u. Dùng th? d? ch?n thành ph? c?a b?n.");
                    break;

                default:
                    actionMessages.Add($"Hi?u ?ng {card.EffectCode} chua du?c h? tr?.");
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

            actionMessages.Add($"{player.Username} t? thi?n {totalPaid:N0} cho các d?i th?.");
        }
        public static void ApplyEarthquakeAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.Type == "City" &&
                            p.OwnerPlayerIndex >= 0 &&
                            p.OwnerPlayerIndex != player.PlayerIndex &&
                            (p.HasHotel || p.HouseCount > 0))
                .OrderByDescending(p => p.HasHotel ? 4 : p.HouseCount)
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có thành ph? d?i th? d? di?u ki?n d? Ð?ng Ð?t phá h?y.");
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

            actionMessages.Add($"Ð?ng Ð?t làm {target.Name} gi?m 1 c?p.");
        }
        public static void ApplyPowerOutageAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.OwnerPlayerIndex >= 0 &&
                            p.OwnerPlayerIndex != player.PlayerIndex &&
                            (p.Type == "City" || p.Type == "Resort"))
                .OrderByDescending(GetCurrentRentUnsafe)
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có d?t d?i th? d? di?u ki?n d? cúp di?n.");
                return;
            }

            target.PowerOutageTurn = gameState.TurnNumber + 2;
            actionMessages.Add($"{target.Name} b? cúp di?n trong 2 lu?t.");
        }
        public static void ApplyMoveChampionshipAutoTargetUnsafe(
                GameState gameState,
                GamePlayerState player,
                List<string> actionMessages)
        {
            GamePropertyState target = gameState.Properties.Values
                .Where(p => p.Type == "City" && p.OwnerPlayerIndex == player.PlayerIndex)
                .OrderByDescending(GetCurrentRentUnsafe)
                .FirstOrDefault();

            if (target == null)
            {
                actionMessages.Add("Không có thành ph? thu?c s? h?u d? d?i Gi?i Vô Ð?ch.");
                return;
            }

            gameState.WorldChampionshipPosition = target.PositionIndex;
            actionMessages.Add($"D?i Gi?i Vô Ð?ch v? {target.Name}.");
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
                    targets.AddRange(gameState.Properties.Values
                        .Where(p => p.Type == "City" &&
                                    p.OwnerPlayerIndex == player.PlayerIndex)
                        .OrderBy(p => p.PositionIndex)
                        .Select(p => p.PositionIndex));
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
                failMessage = "Khong tim thay trang thai game hoac nguoi choi.";
                return false;
            }

            if (!PlayerHasHeldCardUnsafe(player, effectCode))
            {
                failMessage = "Ban khong so huu the nay hoac the da duoc dung.";
                return false;
            }

            if (RequiresCardTarget(effectCode))
            {
                if (!targetPosition.HasValue)
                {
                    failMessage = "The nay can chon muc tieu.";
                    return false;
                }

                List<int> validTargets = BuildCardTargetPositionsUnsafe(gameState, player, effectCode);

                if (!validTargets.Contains(targetPosition.Value))
                {
                    failMessage = "Muc tieu khong hop le cho the nay.";
                    return false;
                }
            }

            switch (effectCode)
            {
                case "FREE_RENT":
                    player.HasFreeRentCard = false;
                    player.IsFreeRentShieldActive = true;
                    actionMessages.Add($"{player.Username} kich hoat Khien Mien Tru cho lan tra tien thue tiep theo.");
                    return true;

                case "ESCAPE_ISLAND":
                    if (!player.IsOnIsland && player.JailTurnsLeft <= 0)
                    {
                        failMessage = "Chi co the dung the thoat Dao Hoang khi dang o Dao Hoang.";
                        return false;
                    }

                    player.HasEscapeIslandCard = false;
                    player.IsOnIsland = false;
                    player.JailTurnsLeft = 0;
                    player.SkipTurnsLeft = 0;
                    player.SkipReason = "";
                    actionMessages.Add($"{player.Username} dung Truc Thang Cuu Ho va thoat Dao Hoang.");
                    return true;

                case "FORCE_DOUBLE":
                    if (gameState.HasRolledThisTurn)
                    {
                        failMessage = "Chi co the dung Xuc Xac Ma Thuat truoc khi do xuc xac.";
                        return false;
                    }

                    if (!player.IsOnIsland && player.SkipTurnsLeft > 0)
                    {
                        failMessage = "Khong the dung Xuc Xac Ma Thuat khi dang bi mat luot.";
                        return false;
                    }

                    player.HasForceDoubleCard = false;
                    gameState.ForceDoubleThisTurn = true;
                    actionMessages.Add($"{player.Username} kich hoat Xuc Xac Ma Thuat cho lan roll nay.");
                    return true;

                case "FLIGHT":
                    return ApplyFlightCardUnsafe(gameState, player, targetPosition.Value, actionMessages, cardDrawEvents, out failMessage);

                case "FREE_UPGRADE":
                    return ApplyFreeUpgradeCardUnsafe(gameState, player, targetPosition.Value, actionMessages, out failMessage);

                case "EARTHQUAKE":
                    return ApplyEarthquakeCardUnsafe(gameState, player, targetPosition.Value, actionMessages, out failMessage);

                case "POWER_OUTAGE":
                    return ApplyPowerOutageCardUnsafe(gameState, player, targetPosition.Value, actionMessages, out failMessage);

                case "MOVE_CHAMPIONSHIP":
                    return ApplyMoveChampionshipCardUnsafe(gameState, player, targetPosition.Value, actionMessages, out failMessage);

                default:
                    failMessage = $"The {effectCode} chua duoc ho tro trong hand.";
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
                failMessage = "O dich khong hop le.";
                return false;
            }

            int oldPosition = player.Position;
            int boardSize = BoardDatabase.Squares.Count;
            player.HasFlightCard = false;
            player.Position = targetPosition;

            if (targetPosition < oldPosition)
            {
                const long startBonus = 300000;
                player.Money += startBonus;
                actionMessages.Add($"{player.Username} bay qua Bat Dau va nhan {startBonus:N0}.");
            }

            actionMessages.Add($"{player.Username} dung Ve May Bay tu o {oldPosition} den o {targetPosition}.");
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
                failMessage = "Khong tim thay o dat can nang cap.";
                return false;
            }

            if (property.Type != "City" ||
                property.OwnerPlayerIndex != player.PlayerIndex ||
                property.HasHotel)
            {
                failMessage = "O dat nay khong the nang cap mien phi.";
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

            actionMessages.Add($"{player.Username} dung Giay Phep Xay Dung nang cap {property.Name} len {DescribePropertyLevelUnsafe(property)} mien phi.");
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
                failMessage = "Muc tieu Dong Dat khong hop le.";
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

            actionMessages.Add($"{player.Username} dung Dong Dat lam {property.Name} giam 1 cap.");
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
                failMessage = "Muc tieu Cup Dien khong hop le.";
                return false;
            }

            player.HasPowerOutageCard = false;
            property.PowerOutageTurn = gameState.TurnNumber + 2;
            actionMessages.Add($"{player.Username} dung Cup Dien lam {property.Name} mat dien trong 2 luot.");
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
                failMessage = "Muc tieu dang cai giai dau khong hop le.";
                return false;
            }

            player.HasMoveChampionshipCard = false;
            gameState.WorldChampionshipPosition = targetPosition;
            actionMessages.Add($"{player.Username} doi Giai Vo Dich ve {property.Name}.");
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
        public static long GetCurrentRentUnsafe(GamePropertyState property)
        {
            if (property == null || property.RentPrices == null || property.RentPrices.Count == 0)
            {
                return 0;
            }

            int rentIndex = property.HasHotel
                ? property.RentPrices.Count - 1
                : Math.Max(0, Math.Min(property.HouseCount, property.RentPrices.Count - 1));

            return property.RentPrices[rentIndex] * Math.Max(1, property.Multiplier);
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
                return "không xác d?nh";
            }

            if (property.HasHotel)
            {
                return "khách s?n";
            }

            if (property.HouseCount > 0)
            {
                return $"{property.HouseCount} nhà";
            }

            return "d?t tr?ng";
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

            List<GamePlayerState> activeHumanPlayers = gameState.Players
                .Where(p => !p.IsBankrupt && !p.IsBot && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (activeHumanPlayers.Count == 1)
            {
                gameState.IsFinished = true;
                gameState.WinnerUsername = activeHumanPlayers[0].Username;
                gameState.HasRolledThisTurn = true;
                gameState.TurnEndsAtUtcTicks = 0;
                actionMessages.Add($"{gameState.WinnerUsername} th?ng tr?n.");
                actionMessages.Add($"X?p h?ng: {BuildRankingSummaryUnsafe(gameState)}");

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

                actionMessages.Add($"Ð?n lu?t {nextPlayer.Username}.");

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

            int releasedProperties = ReleasePlayerPropertiesUnsafe(gameState, player.PlayerIndex);
            actionMessages.Add($"{player.Username} dã phá s?n và m?t {releasedProperties} ô d?t.");

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
            List<GamePlayerState> rankedPlayers = gameState.Players
                .Where(p => !p.IsBot)
                .OrderBy(p => p.IsBankrupt ? 1 : 0)
                .ThenByDescending(p => p.IsBankrupt ? p.BankruptcyOrder : int.MaxValue)
                .ThenBy(p => p.PlayerIndex)
                .ToList();

            List<string> parts = new List<string>();

            for (int i = 0; i < rankedPlayers.Count; i++)
            {
                parts.Add($"#{i + 1} {rankedPlayers[i].Username}");
            }

            return string.Join(", ", parts);
        }
        public static List<GameOverRankingResult> BuildGameOverRankingsUnsafe(GameState gameState)
        {
            List<GamePlayerState> rankedPlayers = gameState.Players
                .Where(p => !p.IsBot)
                .OrderBy(p => p.IsBankrupt ? 1 : 0)
                .ThenByDescending(p => p.IsBankrupt ? p.BankruptcyOrder : int.MaxValue)
                .ThenBy(p => p.PlayerIndex)
                .ToList();

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
        public static GamePlayerState GetNextTurnPlayerUnsafe(GameState gameState)
        {
            List<GamePlayerState> activePlayers = gameState.Players
                .Where(p => !p.IsBankrupt && !p.IsBot && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (activePlayers.Count == 0)
            {
                activePlayers = gameState.Players
                    .Where(p => !p.IsBankrupt && p.IsConnected)
                    .OrderBy(p => p.PlayerIndex)
                    .ToList();
            }

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
                WorldChampionshipPosition = 16,
                IsWaitingForCardChoice = false,
                PendingCardEffectCode = "",
                PendingCardPlayerUsername = "",
                PendingCardTargetPositions = new List<int>(),
                ForceDoubleThisTurn = false
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
                    Money = 2000000,
                    IsBankrupt = false,
                    BankruptcyOrder = 0,
                    IsConnected = !player.IsBot,
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
    }
}



