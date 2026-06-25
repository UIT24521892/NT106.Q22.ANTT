# Monopoly TCP Internet Deployment

## Mục tiêu môn Lập Trình Mạng

Kiến trúc là client/server TCP:

- Unity client gửi request.
- `Monopoly.Server` quản lý room, dice, tiền, vị trí, timer và kết quả.
- Packet JSON kết thúc bằng delimiter `<EOF>`.
- Nhiều client Internet kết nối đến cùng public TCP endpoint.

## Local và LAN

```powershell
dotnet run --project Monopoly.Server -- --port 8080
```

Client đọc `Monopoly.Client.Unity/Assets/StreamingAssets/server-config.json`.

```json
{
  "Host": "127.0.0.1",
  "Port": 8080,
  "ConnectTimeoutSeconds": 8
}
```

LAN dùng IPv4 của máy server, ví dụ `192.168.1.10`.

## Public Internet bằng VPS

1. Chuẩn bị VPS Windows/Linux có public IPv4.
2. Cài .NET 8 Runtime.
3. Publish:

```powershell
dotnet publish Monopoly.Server -c Release -o publish/server
```

4. Chép thư mục `publish/server` lên VPS.
5. Mở inbound TCP `8080` trong firewall và security group.
6. Chạy:

```powershell
dotnet Monopoly.Server.dll --port 8080
```

7. Sửa `StreamingAssets/server-config.json` trong bản Unity build:

```json
{
  "Host": "PUBLIC_IP_OR_DOMAIN",
  "Port": 8080,
  "ConnectTimeoutSeconds": 10
}
```

Client hỗ trợ command line:

```powershell
Monopoly.Client.Unity.exe --server-host PUBLIC_IP --server-port 8080
```

## Port Forwarding tại nhà

- Router forward TCP `8080` đến IPv4 LAN của máy server.
- Windows Firewall cho phép inbound TCP `8080`.
- Client dùng public IPv4 của router.
- Nếu nhà mạng dùng CGNAT thì cần VPS.

## Kiểm thử bắt buộc

- Client A dùng Wi-Fi, client B dùng 4G/5G.
- Cả hai kết nối cùng endpoint.
- Login, create/join room, ready/start hoạt động.
- Dice và game state giống nhau.
- Disconnect rồi resume đúng room.
- Server log đúng remote endpoint.

## Giới hạn hiện tại

- TCP chưa mã hóa TLS.
- Chưa có rate limit theo IP và chống packet flood hoàn chỉnh.
- Trước khi public lâu dài cần TLS, heartbeat, packet-size limit và giám sát server.
