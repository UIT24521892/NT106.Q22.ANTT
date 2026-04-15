## Logic và các điểm đáng chú ý của game
### 1. Khởi Tạo Game & Đổ Xúc Xắc (Setup & Dice Logic)
- Logic đoạn này cần xử lý chặt chẽ bộ đếm thời gian và lượt đi:
- Vốn khởi điểm (Startup Capital): Mỗi người chơi nhận 2,000,000.
- Lượt chơi: Random người đi đầu tiên. Sau đó xoay vòng theo chiều kim đồng hồ.
- Thời gian chờ (Timeout): Nếu người chơi không thao tác đổ xúc xắc trong thời gian quy định, hệ thống tự động đổ. 
- Luật Xúc Xắc Đôi (Double): Nếu đổ ra đôi 3 lần liên tiếp, hệ thống tự động bắt giam quân cờ bay thẳng ra Đảo Hoang (Lost Island).

### 2. Quá Trình Chơi & Nâng Cấp Tòa Nhà (Game Process)
- Hệ số Phạt (Rent Multiplier): Hệ số phạt có thể tăng theo nhiều cách. Tối đa là x10. (Ví dụ: Đất phạt gốc 100, hệ số x2 thì số tiền thực thu là 200).
- Độc quyền màu (Color Monopoly): Khi một người chơi sở hữu tất cả các thành phố của cùng 1 nhóm màu, Hệ số phạt của nhóm đó tự động tăng thêm +1.
- Mua nhà (House): Được phép chọn giữa mua đất/mua 1 nhà/mua 2 nhà/mua 3 nhà trong 1 lượt đi. Ngoại lệ là lượt đầu tiên không được mua 3 nhà
- Xây Khách Sạn (Hotel): Chỉ được phép mua Khách Sạn sau khi ô đất đó đã có 3 Nhà. Tuyệt đối không được mua một lúc 3 Nhà và 1 Khách Sạn trong cùng một lượt.
- Gán nợ: Nếu không đủ tiền trả phạt, người chơi bắt buộc phải bán bớt tài sản hiện có của mình.

### 3. Điều Kiện Giành Chiến Thắng (Objective)
Server cần check liên tục 5 điều kiện này ở cuối mỗi lượt để chốt "Fast Win" hoặc "Time Out":
- Line Monopoly: Sở hữu toàn bộ thành phố trên 1 cạnh của bàn cờ.
- Resort Monopoly: Mua được trọn bộ cả 4 khu du lịch (Resort).
- Triple Monopoly: Sở hữu toàn bộ thành phố của 3 nhóm màu khác nhau trên bàn cờ.
- Hết giờ (Time Out): Nếu không ai đạt 3 điều kiện trên trước khi đồng hồ đếm ngược kết thúc, người chiến thắng là người có Tổng giá trị tài sản (Overall property value) cao nhất.
- Sống sót cuối cùng: Mọi đối thủ đều tuyên bố phá sản.

### 4. Chi tiết Các Ô Đặc Biệt (Special Squares)
Start (Bắt đầu): Mỗi lần đi ngang qua (hoặc giẫm lên), người chơi được nhận 300,000.

Lost Island (Đảo hoang): Bị giam mất 3 lượt. Có 3 cách để hệ thống cho phép thoát:
- Trả phí 200,000.
- May mắn đổ ra xúc xắc đôi (1:1, 2:2, 3:3...).
- Dùng thẻ Cơ Hội đặc biệt (Golden Card) để thoát.

Tax Agency (Cơ quan Thuế): Giẫm vào phải nộp thuế bằng 10% tổng giá trị tài sản của bản thân. Nếu tiền mặt không đủ, phải bán đất để nộp.

Resorts (Khu Nghỉ Dưỡng): Tối đa 4 ô trên bàn cờ, giá mua cố định là 200,000/ô.

World Championships (Giải Vô Địch Thế Giới): Tại 1 thời điểm chỉ tổ chức ở 1 ô. Giẫm vào có các lựa chọn:
- Tăng hệ số phạt tại ô đang tổ chức hiện tại lên +1.
- Bỏ ra 50,000 để dời địa điểm tổ chức sang một thành phố do mình sở hữu (Hệ số cũng được +1).
- Người chơi cũng có quyền từ chối tổ chức.

World Tour (Du Lịch Thế Giới): Lượt tiếp theo, người chơi được bay thẳng đến bất kỳ thành phố nào chưa có chủ. Nếu tất cả đã được mua hết, người chơi được đặt quân cờ lên bất kỳ ô nào của chính mình.

### 5. Kho Thẻ Cơ Hội (Chance Cards Deck)
Trên bàn cờ có 3 ô Chance. Thẻ chia làm 3 phân loại rõ rệt. Một số thẻ Vàng hoặc Gỗ có cơ chế Hold (cất vào kho đồ để chủ động kích hoạt trong các lượt sau).
- Thẻ Vàng (Golden Cards - Luôn có lợi) Mang tính phòng thủ hoặc tạo lợi thế di chuyển cực mạnh:
    + Thẻ Miễn Phí (Shield / Free Rent): Dùng để miễn phí tiền phạt khi giẫm vào khu đất đắt đỏ của đối thủ.
    + Thẻ Bay (Flight): Bay thẳng đến bất kỳ ô trống nào (hoặc ô của mình) trên bàn cờ. Vũ khí tối thượng để chạy nước rút đạt "Fast Win" (nếu bay ngang qua ô Start thì nhận 300,000)
    + Thẻ Ra Đảo (Escape Island): Thoát khỏi Đảo Hoang ngay lập tức mà không tốn 200,000.
    + Thẻ Nâng Cấp: Tự động nâng cấp miễn phí 1 bậc cho thành phố của mình.
- Thẻ Bạc (Silver Cards - Hỗ trợ ngẫu nhiên) Thường được hệ thống ép kích hoạt ngay lập tức:
    + Nhận Tiền (Happy Birthday / Bank Error): Được nhận một khoản tiền ngẫu nhiên từ ngân hàng hoặc ép người chơi khác tặng tiền.
    + Di chuyển ngẫu nhiên: Hệ thống bắt buộc tiến/lùi 1 vài ô hoặc bay thẳng về ô Start (kèm theo nhận 300,000).
    + Đổi vị trí: Buộc người chơi đổi chỗ với một đối thủ ngẫu nhiên trên bàn cờ.
- Thẻ Gỗ (Wooden Cards - Luôn gây hại) Dùng để tấn công đối thủ hoặc chính bạn là nạn nhân khi xui xẻo bốc trúng:
    + Cúp điện (Electricity Outage): Chọn một thành phố của đối thủ để cắt điện. Thành phố đó sẽ mất hiệu lực thu tiền phạt trong một số lượt nhất định.
    + Động Đất (Earthquake / Destroy): Phá huỷ làm tụt 1 cấp độ xây dựng của đối thủ (ví dụ đang có 3 nhà bị đánh tụt xuống 2 nhà). Cực kỳ khó chịu khi phá các Landmark. 
    + Nộp Phạt (Fine): Bị trừ ngay một khoản tiền mặt.

## Physical Online database: entity & detail attribute
Mục đích hướng tới của hệ cơ sở này? Các dữ liệu mới, chưa có ở static data thì mới update lên đây

- USERS: 
    + Personal Info:
        + UserID (PK), Username,  Email,    //This is for personal info show
        + Password is manage by Firebase Authentication (JWT) so it doesn't need to be stored
    + Ranking info:
        + Point, TotalWins, TotalLosses, CreatedAt  //This is for Ranking

- ROOMS: 
    + Room Info:
        + RoomID (PK), Host_UserID (FK), 
        + RoomType, Status, MaxPlayers,  
    + Room Activity:
        + GameEndTime, //Đếm ngược kết thúc trận đấu
        + CurrentTurn_MatchPlayerID (FK), TurnEndTime, 
        + WorldChampionshipPos //Xử lí logic lượt đi. WorldChampionshipPos nên để mặc định là -1 vì PositionIndex của các ô là từ 0-31

- MATCH_PLAYERS: 
    + Player Info:
        + MatchPlayerID (PK), RoomID (FK), UserID, IsBot, 
    + Player Status:
        + Position,  
        + ConsecutiveDoubles, JailTurnsLeft, 
        + IsBankrupt, 
    + Player Asset:
        + TotalAssetValue, Money,
        + OwnedColors,          // đã có completedColorSets còn cần không? Cần, lưu trữ dạng array(VD red-1, blue-1, green-2, khi nào == 3 thì completedColorSet+=1 và ngược lại khi gán nợ)
        + OwnedLines    //kt = gì? Tương tự như OwnedColor, 1 dạng array (kt đủ == số lượng nhà của 1 cạnh thì thôi)
    + Condition Win For Player:
        + ResortCount,          //điều kiện win = 4 resort 
        + CompletedColorSets,   //điều kiện win = 3 completedColorset; kt = thuộc tính ColorSet trong BOARD_SQUARES_CONFIG
        + CompletedLines        //điều kiện win = line 

- MATCH_PROPERTIES: 
    + Property info:
        + PositionIndex (PK),   //Việc để propertyID trùng với PositionIndex để dễ thao tác
        + RoomID (FK), Owner_MatchPlayerID (FK),    
    + Property detail:
        + HouseCount, HasHotel,     //cần cẩn thận viết logic không cho phép từ HouseCount=0 lên HasHotel = true trong cùng 1 lượt
        + Multiplier, PowerOutageTurn   //Liên quan tới tính thêm lệ phí tiền ngoại trừ lệ phí gốc

- INVENTORY_CARDS: 
    + Inventory info:
        + InventoryID (PK), MatchPlayerID (FK), 
        + CardCode (FK) //là ID của CHANCE_CARDS_CONFIG
        + AcquireAt //Có thể dùng để xử lí logic "không được sử dụng ngay trong lượt vừa nhận"

## Static data 
- BOARD_SQUARES_CONFIG: 
    + PositionIndex, Name,          //số thứ tự trên bàn cờ, tên ô 
    + Type,                 //loại ô đất Start, Properties, Chance, Tax, Resort, LostIsland, WorldChampionship
    + BuyPrice, RentPrice   //Giá trị
    + ColorSet              //Nhóm màu, để xử lí logic CompletedColorSet 
    + LineIndex             //Định hình line, xử lí logic OwnedLine

- CHANCE_CARDS_CONFIG: 
    + ID,   //id thẻ, mỗi thẻ 1 id khác nhau, đánh số thứ tự riêng biệt, xử lí logic sẽ dựa vào so sánh EffectCode
    + Name, //Tên thẻ cơ hội, dùng để hiển thị lên màn hình
    + DetailEffect, //Dùng để hiển thị lên màn hình khi bốc dính hoặc sử dụng, hiển thị chung với Name
    + EffectCode    //Hành động thực hiện khi bốc phải lá bài. Tại sao không xử lí = ID luôn? Nhìn ID để viết điều kiện dễ bị lộn, chi bằng so sánh chuỗi string có ghi sẵn hiệu ứng dễ debug hơn
    + Type  //Phân loại card: dùng ngay bây giờ hoặc lưu trữ

Why don't we contain this on database as well, not in the nest folder generated everytime start a new game but a folder like users? Đánh đổi thêm về dung lượng file .exe của Client để giảm bớt latency khi cần phải tải thông tin của các ô về máy

## Authentication token
Mostly JWT - JSON Web Token 
How to apply, do we need to contain password in database anymore? 

Cách áp dụng: Khi sử dụng Firebase Authentication:
- Ứng dụng của bạn sẽ gửi trực tiếp Email/Password mà người dùng nhập cho máy chủ Firebase Auth (chứ không gửi vào Realtime Database).
- Firebase Auth tự động mã hóa, kiểm tra và quản lý mật khẩu này.
- Nếu đúng, Firebase Auth trả về cho ứng dụng của bạn một JWT (ID Token).
- Mỗi khi ứng dụng muốn đọc/ghi vào Realtime Database (ví dụ cập nhật tiền hay đổ xúc xắc), nó phải đính kèm JWT này vào Request.
Nói cách khác, Realtime Database lúc này không cần chứa password và email nữa mà firebase auth sẽ lưu trữ và quản lý riêng, không thể truy cập? True
## communication with database
Use http method: GET (lấy dữ liệu xác thực), PUT - POST (nhập thông tin vào), PATCH (sửa 1 số thông tin)
Create JSON mold C# class for convenient transfer data 
## how to generate different matchplayerID or userID since we only sign in with username, the same go with player
Về UserID: Khi bạn tích hợp Firebase Authentication (như ở Câu 2), mỗi khi một người đăng ký tài khoản mới thành công, Firebase sẽ tự động sinh ra một mã định danh duy nhất (UID - Unique ID) rất dài và ngẫu nhiên (ví dụ: wKx8...aB2). Đây chính là UserID đích thực của bạn. Bạn dùng UID này làm Key trong bảng USERS, còn Username chỉ là một trường hiển thị (Display Name) bên trong đó.

Về MatchPlayerID: Như đã bàn ở trước, trong cấu trúc JSON lồng ghép của Firebase, bạn không nhất thiết phải dùng thuật toán tạo ID phức tạp. Khi một User tham gia vào phòng, bạn dùng chính cái UID của họ (UserID) làm tên nhánh dưới thư mục Players.

Ví dụ: Rooms/Room_1/Players/wKx8...aB2/.

Máy chủ sẽ ngầm hiểu: Người có UID wKx8...aB2 đang đóng vai trò là một MatchPlayer trong phòng Room_1. Điều này giúp hệ thống tự động loại bỏ lỗi trùng lặp dữ liệu mà không cần sinh thêm ID trung gian.
## bot don't have id?
generate new id for it
## Lời khuyên khi bắt tay vào Code TCP C#
Vì bạn chọn con đường code Socket TCP (khó hơn API rất nhiều), bạn hãy đặc biệt lưu ý kỹ thuật "Đóng gói dữ liệu" (Packet Framing):

Mạng TCP là một "dòng chảy liên tục" (Stream). Khi Client gửi 2 hành động liên tiếp, nó có thể dính chùm vào nhau.

Giải pháp: Trong project Monopoly.Shared, hãy tạo một Class chung gọi là NetworkPacket. Mọi tin nhắn gửi đi đều phải chuyển thành chuỗi JSON và có một ký tự kết thúc đặc biệt (Ví dụ: dấu <EOF> hoặc \n).

Bên nhận sẽ đọc liên tục cho đến khi thấy dấu <EOF> thì mới cắt ra để xử lý.
## Cách TCP Server và Client sử dụng JWT (Trả lời thắc mắc của bạn)
Vì bạn đang thiết lập một Server TCP đứng giữa, quy trình bảo mật với JWT sẽ diễn ra như sau:

Client khởi động: Kết nối TCP thành công tới Server.

Client Đăng nhập: Gửi thông tin Email/Pass qua AuthPayload.

Server xử lý: Server nhận gói tin, thay mặt Client gọi Firebase Auth. Firebase trả về cái JwtToken.

Server trả kết quả: Server gửi AuthResponse (có chứa JwtToken) về cho Client thông qua TCP.

Thao tác trong game: Kể từ lúc này, Client lưu JwtToken vào một biến tĩnh (VD: 
public static string UserToken = response.JwtToken;). 
Mỗi lần Client muốn đổ xúc xắc hay gửi tin nhắn chat, nó lại nhét cái JwtToken này vào một trường 
xác thực của gói tin NetworkPacket để Server biết chính xác lệnh này là do ai gửi lên. 
Server sẽ lấy JWT đó, chèn vào URL API (?auth=...) để ghi dữ liệu xuống Firebase một cách hợp lệ.