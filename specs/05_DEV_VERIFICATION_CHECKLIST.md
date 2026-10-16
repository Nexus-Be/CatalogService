# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (CATALOG SERVICE CHECKLIST)

---

## 1. LỆNH BIÊN DỊCH & CHẠY THỬ NGHIỆM

```bash
# 1. Biên dịch solution Catalog
dotnet build Catalog.sln -c Release

# 2. Khởi chạy service (gRPC port 8002)
dotnet run --project Catalog.Api

# 3. Thử nghiệm qua ApiGateway:
# Mở gateway.runtime.json, đổi "catalog": { "Mode": "Real", "Address": "http://localhost:8002" }
# Gọi GET /gateway/api/v1/catalog/products trên Swagger Gateway xem kết quả thật trả về.
```

---

## 2. CHECKLIST TRƯỚC KHI COMMIT CHO AI AGENTS

- [ ] **Biên dịch thành công 100%**: Không lỗi cảnh báo nghiêm trọng.
- [ ] **Không vi phạm rào chắn**: Không dùng Repository Pattern bọc ngoài EF Core.
- [ ] **Port & Database**: Lắng nghe đúng port `8002`, kết nối `category_db` trên port `5433`.
- [ ] **Kiểm tra Concurrency**: Thao tác trừ kho ảo phải đảm bảo chống overselling.
- [ ] **Git Status**: Không commit file rác (`bin/`, `obj/`, `.idea/`, `.env`).
