# Chạy Unit Test và xem Code Coverage

## 1. Di chuyển vào thư mục project

```powershell
cd D:\My-Project\SmartShoppingChatBot
```

## 2. Chạy Unit Test và thu thập Code Coverage

```powershell
dotnet test .\SmartShoppingChatBot.UnitTests\SmartShoppingChatBot.UnitTests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory .\SmartShoppingChatBot.UnitTests\TestResults
```

Kết quả coverage sẽ được lưu trong:

```text
SmartShoppingChatBot.UnitTests\TestResults
```

## 3. Tạo báo cáo Code Coverage chi tiết

```powershell
reportgenerator `
  "-reports:.\SmartShoppingChatBot.UnitTests\TestResults\*\*\coverage.cobertura.xml" `
  "-targetdir:.\SmartShoppingChatBot.UnitTests\CoverageReportApplication" `
  "-reporttypes:Html" `
  "-assemblyfilters:+SmartShoppingChatBot.Application"
```

Báo cáo HTML sẽ được tạo tại:

```text
SmartShoppingChatBot.UnitTests\CoverageReportApplication
```

## 4. Mở báo cáo

Mở file:

```text
CoverageReportApplication\index.html
```

## 5. Cách đọc Code Coverage

* 🟢 **Xanh lá:** dòng code đã được test chạy qua.
* 🔴 **Đỏ:** dòng code chưa được test chạy qua.
* 🟡 **Vàng/Cam:** điều kiện mới chỉ được test một phần các nhánh.
* **Line coverage:** phần trăm số dòng code đã được test.
* **Branch coverage:** phần trăm các nhánh điều kiện (`if`, `else`, `switch`, ...) đã được test.
