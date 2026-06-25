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

### Build phần .NET
Nếu chỉ cần kiểm tra server và shared library:
```bash
dotnet build Monopoly.Server/Monopoly.Server.csproj
```

Nếu muốn build toàn bộ solution:
```bash
dotnet build MonopolyGame.sln
```

Lưu ý: `Monopoly.Client` là Windows Forms app, target `net8.0-windows`. Khi build trên GitHub Actions/Linux/macOS, project đã bật `EnableWindowsTargeting` để tránh lỗi Windows targeting. Nếu cần chạy client WinForms thật sự, hãy chạy trên Windows.

### Unity client
Mở thư mục `Monopoly.Client.Unity` bằng Unity Editor và cài các package cần thiết:
- `com.unity.nuget.newtonsoft-json`
- TextMeshPro
- Unity UI

## Theo dõi tiến độ

File trạng thái chính của project là `NT106_FEATURE_STATUS_AND_TASK_PLAN.md`.

Sau mỗi thay đổi về tính năng, gameplay, network, UI Unity, cấu hình build hoặc sửa lỗi, cần cập nhật file này trong cùng lần thay đổi. Nội dung cập nhật phải gồm:

- Trạng thái tính năng hoặc task liên quan.
- File/code/scene/prefab đã thay đổi.
- Hành vi mới hoặc lỗi đã sửa.
- Kết quả build/test đã thực hiện và phần chưa kiểm tra.
- Một mục trong phần `Nhật ký thay đổi` theo ngày.

Không dùng `README.md` làm changelog chi tiết; file này chỉ trỏ tới tài liệu tiến độ chính.

## 👥 Thành viên thực hiện (Nhóm 4)
* **Hồ Lê Anh Trường** - 24521892
* **Trần Đình Thi** - 24521656
* **Nguyễn Hoàng Nam** - 24521111
