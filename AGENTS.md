<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **CatalogService** (2519 symbols, 4938 relationships, 209 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`; report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- MUST warn on HIGH/CRITICAL `risk` pre-edit; never use `riskSharedAxes` to waive a HIGH/CRITICAL `risk` warning. Compare File/symbol: MCP File omits axes; Graph-RAG expands File.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- **MUST use `query({search_query: "concept"})` for concepts/flows, `context({name: "symbolName"})` for a named symbol, or `impact` for blast radius, on read-only callers, dependencies, imports, or execution flow.** Graph first; text search only for empty/`UNKNOWN`/literals.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/CatalogService/context` | Codebase overview, check index freshness |
| `gitnexus://repo/CatalogService/clusters` | All functional areas |
| `gitnexus://repo/CatalogService/processes` | All execution flows |
| `gitnexus://repo/CatalogService/process/{name}` | Step-by-step execution trace |

<!-- gitnexus:end -->

---

# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (CATALOG SERVICE)

> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA — 100% RESTful API (`CategoriesController`, `ProductsController`), Feature Co-location, cấm gRPC, bãi bỏ hoàn toàn RabbitMQ, MassTransit và Outbox tables.
> **Nguồn sự thật:** code (`Catalog.sln`, `Catalog.Api/Controllers/CategoriesController.cs`, `Catalog.Api/Controllers/ProductsController.cs`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## Endpoint Workflow

Before adding or changing any REST endpoint, follow the local checklist at `.agents/skills/new-feature-endpoint/SKILL.md`. It covers contract, persistence, Application, Api, tests, documentation handoff, and verification. This repository is Inward-Only, so the checklist is self-contained.

---

## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)

Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại CatalogService **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor, tạo migration database hay thay đổi cấu hình nào.

Một Plan chuẩn bắt buộc phải có:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của CatalogService theo ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**: Dùng GitNexus `impact`.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet build Catalog.sln -c Release`.

---

## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)

CatalogService là một Git Repository độc lập:
* **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của CatalogService**.
* **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../ApiGateway/...`).
* Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).

---

## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI CATALOG SERVICE

Khi bắt đầu làm việc tại CatalogService, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context danh mục, sản phẩm, kho ảo; port `8002`; DB `5434` (`category_db`).
2. `specs/01_CLEAN_ARCHITECTURE_LAYERS.md`: Cấu trúc 4 tầng Clean Architecture, No-Repository pattern over EF Core.
3. `specs/02_API_REST_CONTRACTS.md`: Hợp đồng RESTful API: `CategoriesController`, `ProductsController`.
4. `specs/03_EVENT_MESSAGING.md`: Trạng thái bãi bỏ RabbitMQ / MassTransit; giao tiếp REST trực tiếp.
5. `specs/04_DATA_PERSISTENCE.md`: Schema PostgreSQL thật (`Categories`, `Products`, `ProductVariants`...).
6. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Lệnh build & smoke test kiểm chứng service.

---

## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI CATALOG SERVICE

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**: Sử dụng trực tiếp `AppDbContext` và `DbSet<T>` (hoặc interface `ICatalogDbContext`) trong tầng Application. Cấm tạo `IProductRepository`, `ICategoryRepository`, `UnitOfWork`.
2. **CẤU HÌNH PORT 8002 VÀ DATABASE 5434 LÀ BẤT BIẾN**.
3. **CẤM SỬ DỤNG gRPC VÀ BÃI BỎ MESSAGE BROKER — BẮT BUỘC DÙNG 100% RESTFUL API**:
   - Toàn bộ giao tiếp đồng bộ sử dụng chuẩn **RESTful Web API** (JSON qua HTTP/1.1 hoặc HTTP/2) với quy chuẩn 100% số nhiều cho collections: `/api/v1/products`, `/api/v1/categories`, `/api/v1/variants`.
   - Tuyệt đối cấm gRPC, Protobuf, RabbitMQ và MassTransit.
4. **FEATURE CO-LOCATION & 1-TO-1 CONTROLLER PAIRING**:
   - Tầng Application: Toàn bộ use cases đặt tại `Catalog.Application/Services/{Feature}/` gồm `I{Feature}Service.cs`, `{Feature}Service.cs`, `{Feature}Requests.cs`, `{Feature}Responses.cs`, `{Feature}Dtos.cs`.
   - Tầng Api: Ghép cặp 1-to-1 với `CategoriesController.cs` và `ProductsController.cs`, chỉ inject duy nhất feature service tương ứng.
5. **QUY CHUẨN PHÂN TRANG (PAGINATION GOVERNANCE)**:
   - Toàn bộ thao tác phân trang sản phẩm, danh mục bắt buộc thực hiện trực tiếp tại Database qua `.Skip((page - 1) * pageSize).Take(pageSize)` và `.CountAsync()`.
   - Đóng gói dữ liệu trả về theo cấu trúc phân trang chuẩn `PagedData<T>` (`items` và metadata `pagination: { pageIndex, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage }`).
   - TUYỆT ĐỐI CẤM trả về danh sách thô lớn không giới hạn để Gateway tự phân trang.
6. **INTERNAL API ĐANG TẠM KHÓA (2026-10-31)**:
   - Route liên service dùng đường dẫn công khai, không còn `/api/v1/internal`.
   - `InternalServiceAuthMiddleware` và `InternalServiceAuthHandler` đã comment đăng ký; không yêu cầu `X-Internal-Service-Key`.
7. **QUY CHUẨN BẢO VỆ QUYỀN SỞ HỮU TÀI NGUYÊN (RESOURCE OWNERSHIP GOVERNANCE)**:
   - Khi tạo hoặc chỉnh sửa sản phẩm, `SellerId` bắt buộc trích xuất từ `HttpContext.GetUserId()`.
   - Người bán chỉ được phép thao tác trên các sản phẩm thuộc sở hữu của gian hàng mình; thao tác phê duyệt hoặc can thiệp của admin bắt buộc kiểm tra quyền `admin` hoặc `staff`.

---

## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG (MANDATORY ERROR & ENUM RULES)

1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi viết bằng 100% tiếng Anh. Tuyệt đối CẤM dùng tiếng Việt trong response và runtime exception.
2. **Quy cách viết hoa thông báo lỗi (100% Sentence case)**:
   - Viết hoa DUY NHẤT chữ cái đầu tiên của câu thông báo.
   - Toàn bộ các từ tiếp theo viết thường, TRỪ các từ viết tắt kỹ thuật hoặc tên riêng: `ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`, `API`, `HTTP`.
   - TUYỆT ĐỐI CẤM viết hoa toàn bộ thông báo lỗi (ví dụ cấm: `"PRODUCT NOT FOUND"`).
   - Ví dụ đúng: `"Product not found in the system."`, `"Category not found with the specified ID."`
3. **Mã lỗi (100% SCREAMING_SNAKE_CASE)**:
   - Viết hoa toàn bộ và nối bằng gạch dưới: `PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.
   - CẤM tiền tố `ERR_`. Tiền tố `VAL_` chỉ dành riêng cho validation.
4. **Quy chuẩn Tuần tự hóa Enum đầu ra REST API (PascalCase Serialization)**:
   - 100% Enum trả về trong JSON envelope phải là chuỗi `PascalCase` (`"Active"`, `"Draft"`), cấm chuỗi in hoa toàn bộ hoặc số nguyên.
5. **Cơ chế Duy trì Rule Bền vững ("Ghi vào Rule")**:
   - Khi Người dùng (Sếp) chỉ thị "ghi vào rule", AI Agent bắt buộc phải lập tức ghi nhận và đồng bộ vào file này, `.agents/AGENTS.md`, root `AGENTS.md` và `.cursor/rules/`.

---

## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)

Một task tại CatalogService chỉ hoàn tất khi:
* Đã lập Plan chi tiết trước khi thực hiện.
* Không chứa bất kỳ liên kết trỏ ngược ra ngoài CatalogService.
* `dotnet build Catalog.sln -c Release` thành công 100% (0 error).
* Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`).
