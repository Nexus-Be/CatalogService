<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **CatalogService** (384 symbols, 490 relationships, 4 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows. For regression review, compare against the default branch: `detect_changes({scope: "compare", base_ref: "main"})`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method without first running `impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit changes without running `detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/CatalogService/context` | Codebase overview, check index freshness |
| `gitnexus://repo/CatalogService/clusters` | All functional areas |
| `gitnexus://repo/CatalogService/processes` | All execution flows |
| `gitnexus://repo/CatalogService/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->

---

# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (CATALOG SERVICE)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 13 action REST mock, **0/13 rpc gRPC**, 9 bảng nghiệp vụ + 3 bảng outbox, **0 event**, **0 test**.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

---

## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)

Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại CatalogService **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor, tạo migration database hay thay đổi cấu hình nào.

Một Plan chuẩn bắt buộc phải có:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của CatalogService cần đọc theo đúng ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**:
   - Dùng GitNexus `impact` đánh giá blast radius các symbol bị ảnh hưởng.
   - Cảnh báo rõ nếu mức độ rủi ro là HIGH hoặc CRITICAL.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**: Từng bước logic, chi tiết.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet build` và `dotnet test` tương ứng.

---

## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)

CatalogService là một Git Repository độc lập (`CatalogService.git`):
- **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của CatalogService**.
- **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../ApiGateway/...`).
- Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).

---

## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI CATALOG SERVICE

Khi bắt đầu làm việc tại CatalogService, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context danh mục, sản phẩm, kho ảo; port `8002` (Docker) / `5272` (local); DB `5434` (`category_db`).
2. `specs/01_CLEAN_ARCHITECTURE_LAYERS.md`: Cấu trúc 4 tầng Clean Architecture, No-Repository pattern over EF Core.
3. `specs/02_API_GRPC_CONTRACTS.md`: **Đặc tả mục tiêu** gRPC `CatalogApi` (13 rpc — ❌ chưa triển khai) và bảng đối chiếu sang 13 action REST mock hiện có.
4. `specs/03_EVENT_MESSAGING.md`: Sự kiện RabbitMQ dự kiến (`StockReservedEvent`, `StockReleasedEvent`… — ❌ 0 consumer/0 publish).
5. `specs/04_DATA_PERSISTENCE.md`: Schema PostgreSQL thật (12 bảng: 9 nghiệp vụ + 3 outbox; ❌ không có bảng `inventories`).
6. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Lệnh build & smoke test kiểm chứng service.

---

## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI CATALOG SERVICE

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**: Sử dụng trực tiếp `AppDbContext` và `DbSet<T>` (hoặc interface `ICatalogDbContext`) trong tầng Application. Cấm tạo `IProductRepository`, `ICategoryRepository`, `UnitOfWork`. (Hiện trạng đúng: `Catalog.Application/Interfaces/ICatalogDbContext.cs:8-21`.)
2. **KHÔNG NHẦM LẪN KHO ẢO (CATALOG) VỚI KHO VẬT LÝ (FULFILLMENT)**: CatalogService chỉ quản lý tồn kho ảo sẵn sàng bán (`Available`). Logic vận chuyển, kiện hàng, mã tracking thuộc về FulfillmentService. ⚠️ Hiện repo **không có entity/bảng tồn kho nào** — `Available`/`Reserved` chỉ tồn tại trong `InventoryDto` in-memory của `Catalog.Api/Mock/MockCatalogStore.cs:11-15`.
3. **CẤU HÌNH PORT 8002 VÀ DATABASE 5434 LÀ BẤT BIẾN**: `8002` là port đặc tả Docker (`Dockerfile:3-4`); chạy local bằng `dotnet run` là `5272`/`7232` (`Catalog.Api/Properties/launchSettings.json:17,27`). ❌ Không dùng `5099` (giá trị cũ còn sót trong `Catalog.Api/Catalog.Api.http:1`). DB: `category_db` port `5434` (`docker-compose.yml:19`).
4. **HỢP ĐỒNG PROTOBUF**: ❌ Hiện **không tồn tại** `Contracts/shop.proto`, không có package `Grpc.*`, không có `CatalogGrpcService`, nên **không có hợp đồng nào để đồng bộ với Gateway**. Bề mặt khả dụng duy nhất là REST `/api/v1/catalog/*`. Cấm tự tạo/sửa `.proto` khi chưa chốt hợp đồng liên service — hãy coi `specs/02_API_GRPC_CONTRACTS.md` là đặc tả mục tiêu.
5. **CẤM TỰ Ý SỬA ĐỔI BIẾN MÔI TRƯỜNG, SECRET VÀ KEY BÍ MẬT (MANDATORY HUMAN-IN-THE-LOOP FOR SECRETS)**:
   - **Tuyệt đối CẤM mọi AI Coding Agent tự ý sửa đổi, thay thế, xóa hoặc ghi đè các biến môi trường (`.env`, `.env.*`), Connection Strings, API Keys, JWT Secrets, chứng chỉ hoặc bất kỳ thông tin bí mật (credentials/secrets) nào trong toàn bộ hệ thống.**
   - **Bắt buộc phải xin ý kiến và nhận được sự đồng ý rõ ràng (explicit approval) từ người dùng trước khi thực hiện bất kỳ thay đổi nào liên quan đến biến môi trường hoặc tài khoản bí mật.**
   - Khi phát hiện cấu hình môi trường, connection string hoặc secret key có vấn đề hoặc cần cập nhật, Agent **CHỈ ĐƯỢC PHÉP báo cáo hiện trạng, đề xuất phương án và chờ người dùng xác nhận**, tuyệt đối không tự ý can thiệp.

---

## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG (MANDATORY ERROR RULES)

Tất cả các thông báo lỗi (Error Messages, Exceptions, ProblemDetails, Validation Errors, Response Details) **BẮT BUỘC** phải tuân theo 3 nguyên tắc sau:
1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi phải được viết bằng tiếng Anh. Tuyệt đối không dùng tiếng Việt trong chuỗi thông báo lỗi trả về cho client.
2. **Quy cách viết hoa (Sentence case)**: Viết hoa duy nhất chữ cái đầu câu, các từ tiếp theo viết thường (ngoại trừ tên riêng, từ viết tắt hoặc định danh kỹ thuật như `ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`...).
   - ✅ Đúng: `"Product not found in the system."`
   - ✅ Đúng: `"Insufficient stock available for the requested product."`
   - ❌ Sai: `"Sản phẩm không tồn tại"` (Không dùng tiếng Việt)
   - ❌ Sai: `"invalid product id"` (Chữ cái đầu không viết hoa)
   - ❌ Sai: `"INVALID PRODUCT ID"` (Cấm viết hoa toàn bộ)
   - ❌ Sai: `"Invalid Product Id Provided"` (Không dùng Title Case)
3. **Mã lỗi (Error Codes — SCREAMING_SNAKE_CASE)**: Mã lỗi luôn viết hoa toàn bộ và nối bằng dấu gạch dưới:
   - Ví dụ: `PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `INSUFFICIENT_STOCK`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.
   - ⚠️ Hiện trạng: `ApiResponseFactory` trả `application/json` với `ApiErrorResponse`, còn `GlobalExceptionHandlerMiddleware` trả `application/problem+json` với anonymous object ⇒ **2 phong bì lỗi song song**. Khi thêm endpoint mới, phải dùng `IApiResponseFactory`, không tự dựng object lỗi.

---

## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)

Một task tại CatalogService chỉ hoàn tất khi:
- [ ] Đã lập Plan chi tiết trước khi thực hiện.
- [ ] Không chứa bất kỳ liên kết trỏ ngược ra ngoài CatalogService.
- [ ] `dotnet build Catalog.sln -c Release` thành công 100% (0 error). (⚠️ Tên solution thật là **`Catalog.sln`** — không phải `CatalogService.sln` như bản tài liệu cũ ghi.)
- [ ] `dotnet test Catalog.sln -c Release` vượt qua 100% các bài test — ⚠️ hiện **0 project test**, lệnh này không chạy bài nào; đừng báo "đã pass test" khi chưa có test.
- [ ] Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`).

---

## 🧪 NGUYÊN TẮC 7: HIỆN TRẠNG REPO (BẮT BUỘC ĐỌC TRƯỚC KHI CODE)

> Số liệu dưới đây đo trực tiếp trên repo này ngày 2026-10-24: **49 file `.cs` / 2.050 dòng** (không tính `Migrations/`, `obj/`, `bin/`; nếu tính 3 file migration là 52 file), 4 project, 0 project test.

1. **Bề mặt dịch vụ**: ✅ 13 action REST dưới `/api/v1/catalog/*` + `GET /health` (`Program.cs:66-69`). ❌ **0 gRPC** (`0 file .proto`, 0 package `Grpc.*`, 0 `CatalogGrpcService`).
2. **Tầng dữ liệu**: ✅ EF Core 8 + Npgsql 8.0.10, `AppDbContext` với 9 `DbSet`, 9 `EntityTypeConfiguration`, 1 migration `20261024103127_InitialCatalogSchema` tạo **12 bảng**, seed `HasData` 25 row.
3. **API không dùng database**: ❌ 3 controller inject `MockCatalogStore` (singleton, 211 dòng, nạp `Catalog.Api/Seeds/*.json` trong ctor, `catch { }` rỗng ở `:60-63`). `IProductService`/`ICategoryService` được đăng ký DI nhưng **0 nơi inject** ⇒ dead code.
4. **Kho ảo là mock**: ❌ Không có entity `Inventory`; `GetInventory` trả `Available=100` cho product không tồn tại (`MockCatalogStore.cs:193`); `AdjustInventory` tạo mới với `Available=100` (`:203`).
5. **Auth**: ❌ 0 `[Authorize]`, 0 package JWT — mọi POST/PUT/DELETE công khai, không kiểm tra seller sở hữu.
6. **Messaging**: ⚠️ MassTransit 8.3.0 + EF Outbox đã đăng ký (`Catalog.Infrastructure/DependencyInjection.cs:23-48`) nhưng **0 consumer, 0 publish**.
7. **Hai nguồn seed id khác nhau**: EF `HasData` (`10000000-…`, `11000000-…`, seller `cccccccc-…`) vs mock JSON (`aaaaaaaa…`, `2222…`, seller `1111…`) — không tham chiếu chéo được.
8. **Rủi ro chuỗi cung ứng**: ⚠️ `Directory.Build.targets:2-4` tự ký **mọi** `*.dll` trong `TargetDir` bằng cert `CatalogDev` (Windows), `IgnoreExitCode`+`ContinueOnError` ⇒ ký cả DLL bên thứ ba và nuốt lỗi.
9. **Bí mật & cấu hình chết**: ⚠️ `appsettings.json:10,16` chứa mật khẩu plaintext (file vẫn tracked dù `.gitignore` chặn); `Otel.Endpoint` và `OTEL_EXPORTER_OTLP_ENDPOINT` không được code nào đọc (không có package OpenTelemetry).
10. **Dead code khác**: `CatalogDbContext` (`AppDbContext.cs:37-41`) không được đăng ký DI; 3 exception domain có khai báo nhưng **0 lệnh `throw new`** toàn repo.
11. **CI**: `.github/workflows/ci-cd.yml` chỉ build/push Docker image, **không chạy `dotnet test`**.

**Quy tắc rút ra:**
- Không được coi `/health` là bằng chứng "service hoạt động": nó trả hằng số, không kiểm tra DB (`Program.cs:66`), và DB lỗi chỉ bị nuốt bằng `LogWarning` (`Program.cs:50-64`).
- Mọi tài liệu khi sửa **phải có banner trạng thái** (✅ CÓ / ⚠️ MỘT PHẦN / ❌ CHƯA CÓ + nguồn sự thật + ngày) và bảng liệt kê phải có cột `Trạng thái` + `Bằng chứng`.
- Trước khi viết code nghiệp vụ mới: phải quyết định dứt điểm 3 việc — (a) giữ REST hay chuyển gRPC, (b) chuyển controller sang `ICatalogDbContext`, (c) có tạo entity tồn kho thật hay không.
