# 🎲 Monopoly Online - Network Programming Project

## 📝 Giới thiệu dự án
Đồ án môn **Lập trình mạng** - Xây dựng trò chơi Cờ Tỷ Phú (Monopoly) đa người chơi dựa trên kiến trúc Client-Server. Dự án mô phỏng bàn cờ tỷ phú truyền thống với các tính năng tương tác thời gian thực qua mạng TCP/IP.

## 🚀 Build và chạy

### Yêu cầu
- .NET SDK 8.0
- Unity Editor cho project `Monopoly.Client.Unity`

### Chạy server
```bash
dotnet run --project Monopoly.Server/Monopoly.Server.csproj
```

### Chạy test gameplay
```bash
dotnet run --project Monopoly.Server/Monopoly.Server.csproj -- --run-tests
```

### Build phần .NET
```bash
dotnet build Monopoly.Server/Monopoly.Server.csproj
```

### Unity client
Mở thư mục `Monopoly.Client.Unity` bằng Unity Editor. Project dùng Unity `2022.3.62f3`.

## Theo dõi tiến độ

File trạng thái chính của project là `NT106_FEATURE_STATUS_AND_TASK_PLAN.md`.

Sau mỗi thay đổi về tính năng, gameplay, network, UI Unity, cấu hình build hoặc sửa lỗi, cần cập nhật file này trong cùng lần thay đổi. Nội dung cập nhật phải gồm trạng thái task, file đã đổi, hành vi mới và kết quả build/test.

## 👥 Thành viên thực hiện (Nhóm 4)
* **Hồ Lê Anh Trường** - 24521892
* **Trần Đình Thi** - 24521656
* **Nguyễn Hoàng Nam** - 24521111
