# SendMessage V3 implementation plan

## Scope and rules

- Copy the original SendMessage and KernelChatService into V3. Preserve chat credit calculation, token extraction, business checks, persistence order, analytics events, and config reads.
- Use V3 suffixes for all new files, folders and classes. The agreed exceptions are IProductSemanticSearchByAI and ProductSemanticSearchByAI.
- Keep existing implementations intact. Program.cs selects the new default product plugin; an isolated legacy kernel keeps the old endpoints working.
- Implement product operations in a service, without product CQRS handlers. The copied SendMessage entry point retains its existing MediatR contract.
- Use plain English comments. No new unit tests or live API tests are included.

## Implementation order

1. Introduce ProductReferenceV3 for tool results, collector snapshots, selected products, conversation turns and reference resolution. Convert only at the existing public response and storage boundaries. Reload product facts from the current business when restoring historical references.
2. Copy SendMessage into Features/ConversationManagement/SendMessageV3 with the same quota calculation, transaction order, messages and published event content.
3. Copy KernelChatService into KernelChatServiceV3, keeping ChatTokenUsage extraction and ModelTemperature/MaxOutPutToken config behavior. Load SemanticKernelSystemV3.md and inject active category schemas for every V3 chat.
4. Add direct-service product tools in ProductPluginV3: search, product details, up/down-sell and complementary products.
5. Implement category, tenant, status, price and explicit strict-attribute filters. Validate schema keys and allowed values server-side. Use the existing technical/semantic vectors and reranker.
6. Map Low, Medium and High to the four existing BusinessConfig price fields. Explicit numeric budgets take precedence over vague price words. Keep the old relative price windows for up/down-sell and intersect them with any explicit budget.
7. Search one to three cross-sell targets separately. Exclude the reference product and deduplicate results; keep successful groups when another group fails. Balance results across groups and honor the configured total TopKDocument.
8. Expose POST /api/v3/chat/conversations/messages and POST /api/v3/chat/conversations/{conversationId}/messages with the existing response envelope and API key authentication.
9. Register V3 services, comment the old product plugin lines in Program.cs, and include the V3 prompt in build/publish output.
10. Compare preserved sections against the source, run diff checks, and run the project's clean-build workflow. Record build results separately from live behavior validation.

## Config behavior

| Concern | Source |
| --- | --- |
| Chat temperature / output limit | Business.Config.ModelTemperature / MaxOutPutToken, with original fallbacks |
| Business prompt / fallback text | Business.Config.SystemPrompt / FallBackMessage |
| Rerank ordering / result limit | Accept every reranker score for ordering; TopKDocument from RedisBusinessConfig, then business config |
| Low price | 0 through LowPriceMaxLimit |
| Medium price | MediumPriceMinLimit through MediumPriceMaxLimit |
| High price | HighPriceMinLimit and above |
| Relative down-sell | 85% of live reference price through reference price minus 0.01 |
| Relative up-sell | Reference price plus 0.01 through 120% of reference price |
| Chat credits | InputTokens + OutputTokens * 6 |
| Recent conversation turns | RedisOptions.RecentTurnLimit |

The four configured price fields define three bands, not four bands. "Bình dân" maps to Low; "tầm trung" maps to Medium. Price-band boundaries remain inclusive as configured.

## Deployment and manual acceptance

- The existing collection and named vectors are reused. Category filtering requires the canonical category payload written by ProductEmbedCommandHandler; older points missing that payload require backfilling/re-embedding.
- The schema list describes allowed categories; a business may have no active products in some of them. Return an empty group rather than inventing inventory.
- Keep the existing database and Redis formats so old and V3 conversations remain readable. Full V3 references are hydrated on each load instead of changing stored message entities.
- Check: generic cheap/expensive queries, cheap trousers, yellow evening trousers under the configured limit, numeric-budget overrides, cheaper/more expensive replacements, ambiguous source references, one-category cross-sell, multi-category cross-sell with an empty/failing group, canonical IDs, tenant isolation and config limits.
- Build is an offline correctness check. Real model relevance, Qdrant payload quality and full API behavior need a configured running stack and a separate smoke-test run.

## Verification record

- Clean/restore/build completed with .NET SDK 8.0.404: zero errors. The solution reports warnings, including existing nullable-reference and API documentation warnings.
- The final incremental build after BM25 and V3 controller changes completed with zero errors. `git diff --check` found no whitespace errors.
- Source comparison confirmed unchanged billing/commit logic, event construction (apart from V3 reference types and resolver names), customer/business checks, and ChatTokenUsage extraction.
- Original SendMessageCommandHandler.cs and KernelChatService.cs still match their snapshots taken before implementation. Existing user changes in KernelChatService.cs, InfrastructureDI.cs and SendMessageV2 were retained.
- The V3 prompt is copied to the API output. No unit tests or live API requests were run.
- BM25 is queried only when the existing product collection and points contain the configured sparse vector. Older data falls back to the existing two-dense retrieval; no automatic re-embedding/backfill was performed.
- V3 reuses the existing `QdrantCollectionInitializer` and product collection. It does not create or register a separate V3 initializer.

## Roadmap nâng chất lượng V3 — 2026-09-22

**Trạng thái: đang triển khai theo từng đợt.** Các mục phía trên ghi lại bản V3 ban đầu; phần dưới là kế hoạch nâng cấp sau khi đọc code và tham khảo tài liệu chính thức. Không xem kết quả build trước đó là bằng chứng chất lượng tìm kiếm của roadmap này.

Đợt hiện tại triển khai: bảo vệ canonical category khi cập nhật payload, lọc cứng thuộc tính khách truyền, phân biệt sắp giá cực trị, thêm BM25 vào RRF với fallback dense, bỏ fallback rerank 0.25, thêm dữ kiện sản phẩm nguồn cho cross-sell và hoàn thiện hai API chat V3. Backfill BM25 cho point cũ và benchmark relevance cần môi trường/dữ liệu vận hành riêng.

Đã bổ sung đường duyệt category riêng cho câu hỏi không có nhu cầu semantic, ví dụ “cho tôi xem vài sản phẩm quần” hoặc “cho tôi xem quần giá rẻ”. Đường này dùng collection Qdrant cũ, lọc tenant/status/category/giá, xác minh lại database và không gọi embedding hoặc reranker.

V3 hiện nhận mọi kết quả mà reranker trả về, không loại theo `RerankingScore`. Score chỉ quyết định thứ tự; prompt buộc AI kiểm tra dữ kiện sản phẩm và được phép không chọn ứng viên nào nếu không đáp ứng yêu cầu.

### 1. Mục tiêu và ranh giới

- Ưu tiên đúng sản phẩm, đúng điều kiện, đúng tham chiếu hội thoại; sau đó tối ưu tốc độ, chi phí và tỷ lệ mua kèm. Không mặc định thêm model/tool call là tốt hơn.
- Giữ một `ProductReferenceV3` xuyên suốt plugin, collector, resolver, context và SendMessage. DTO mô tả yêu cầu/kết quả tìm kiếm không được trở thành một bản sao product reference khác.
- Giữ nguyên công thức tính phí, cách đọc token, quota, transaction, lưu message và publish event. Telemetry tìm kiếm mới phải tách khỏi billing và không đổi nội dung event cũ.
- Giữ cách đọc Business config hiện tại. Bốn mốc giá hiện có tạo **ba phân khúc** Low/Medium/High; không tự tạo phân khúc thứ tư. Giữ cửa sổ giá up/down-sell và quy tắc ngân sách số đã có.
- Mọi file/folder/class mới có hậu tố V3, trừ hai service đã được Mahiru cho phép. Logic sản phẩm nằm trong `ProductSemanticSearchByAI`; các adapter mới chỉ phụ trách giao tiếp hạ tầng. Không thêm product CQRS.
- Không sửa/xóa code cũ để thực hiện roadmap. Nếu cần thay đổi writer cũ, config entity cũ hoặc hạ tầng production, phải nêu riêng và xin quyết định trước. Không tự nâng Qdrant hay đổi nhà cung cấp model.
- Không làm unit test. Dùng bộ tình huống đánh giá chất lượng, kiểm tra hồi quy phần cần giữ nguyên và clean build khi triển khai code. Chỉ chạy kiểm thử API khi Mahiru cho phép Docker Compose hoặc xác nhận stack đang chạy.

### 2. Các điểm yếu đã thấy trong code

| Ưu tiên | Bằng chứng trong repo | Ảnh hưởng |
| --- | --- | --- |
| P0 | Handler embed ghi category chuẩn theo schema, nhưng `ProductUpdateCommandHandler` gọi `BuildQdrantPayload(product)` khi sửa giá không cần embed; mapping ghi lại category nguồn | Có đường chạy ghi đè category chuẩn nếu hai category khác nhau; chưa xác nhận dữ liệu production đã bị ảnh hưởng |
| P0 | `ApplyAttributes` chỉ lọc cứng khi schema có `IsStrict=true` | Thuộc tính khách yêu cầu bắt buộc có thể chỉ được đưa vào câu embedding |
| P0 | Không có sort mode; tìm semantic lấy tối đa 40 candidates trước khi rerank | Không đủ để kết luận một sản phẩm là “rẻ nhất” toàn bộ tập hàng phù hợp |
| P1 | `QdrantService.HybridSearchAsync` chỉ kết hợp hai dense vectors; BM25 được chuẩn bị khi embed nhưng không truy vấn ở đây | Chưa khai thác nhánh khớp từ khóa/mã hàng |
| P1 | Service dùng ngưỡng config rồi tự hạ xuống `0.25` khi không đạt | Có thể trả kết quả yếu mà không phân biệt mức khớp |
| P1 | Cross-sell tìm từng target và rerank query–product, chưa có bước kiểm tra cặp source–candidate | Đúng category chưa chứng minh phù hợp để mua kèm |
| P2 | Kernel đưa toàn bộ schema active vào prompt; context hydrate toàn bộ tham chiếu gần đây | Có cơ hội giảm dữ liệu không liên quan và tăng tính ổn định khi catalogue lớn |
| P2 | Prompt vừa yêu cầu giữ điều kiện bắt buộc, vừa cho phép đưa lựa chọn gần nhất khi không khớp | Cần một quy tắc thống nhất cho kết quả chính xác, phương án thay thế và lỗi tra cứu |

### 3. Luồng xử lý đích

```text
Tin nhắn + trạng thái hội thoại + ProductReferenceV3
    -> AI tạo yêu cầu có cấu trúc / chọn tool
    -> Service xác thực nguồn, category, điều kiện và config
    -> Chọn đường tìm: ID/mã hàng | lọc + sắp giá | hybrid
    -> Kiểm tra dữ liệu hiện hành + xếp hạng phù hợp
    -> Nếu mua kèm: kiểm tra cặp sản phẩm và ngân sách nhóm
    -> Kết quả có mức khớp, bằng chứng, điều kiện chưa đạt
    -> AI diễn đạt -> xác thực ID đầu ra -> SendMessageV3 như cũ
```

Không bắt buộc thêm một lượt LLM “planner” cho mọi tin nhắn. Trước tiên mở rộng arguments của tool hiện tại thành yêu cầu có cấu trúc; backend quyết định đường thực thi. Chỉ cân nhắc lượt phân tích riêng nếu đánh giá thực tế chứng minh cần thiết.

### 4. P0 — Đúng dữ liệu và đúng điều kiện trước

**4.1. Ổn định canonical category và vòng đời index**

- Kiểm kê các đường create/embed/update/delete trước khi thay đổi retrieval. Đối chiếu Mongo product, category nguồn, schema chuẩn và payload; báo số bản ghi thiếu/sai, không tự đoán category từ chuỗi nguồn.
- Chỉ sửa payload một lần là chưa đủ: writer cũ có thể ghi đè lần sau. Thêm một field V3 trong collection cũ cũng chưa bảo đảm an toàn nếu full upsert thay toàn bộ payload.
- Với yêu cầu không sửa writer cũ, hướng cách ly là một collection tìm kiếm V3 do thành phần V3 sở hữu. Đây là thay đổi hạ tầng cần duyệt trước triển khai, không tự tạo trong lượt lập plan. Đánh giá chi phí lưu trữ trước khi chọn; không bắt buộc đổi embedding model.
- Thành phần đồng bộ V3 đọc sản phẩm đã embed thành công, tái sử dụng vector tương thích, lưu schema/category/phiên bản nguồn riêng. Khi không thể khôi phục category chuẩn đáng tin cậy, đánh dấu cần chuẩn hóa, không copy category đã nghi sai. Việc phân loại/embed bổ sung phải được dự toán riêng, không lách quota cũ.
- Đồng bộ tăng dần theo phiên bản/UpdatedAt, có checkpoint, xử lý lặp an toàn, chống ghi phiên bản cũ lên mới và đối soát định kỳ cho sản phẩm bị xóa. Không sửa hợp đồng hoặc thứ tự publish event cũ. Chốt cơ chế phát hiện cập nhật/xóa theo khả năng DB thực tế trước khi xây worker.
- Backfill theo batch, kiểm tra độ phủ/độ trễ, chạy đối chiếu rồi mới chuyển V3 bằng config. Giữ collection cũ và đường rollback; chưa cho phép collection chưa sẵn sàng phục vụ thay bản hiện tại.
- Giá/trạng thái cuối cùng vẫn xác thực bằng DB. Index cũ có thể gây bỏ sót dù đã kiểm tra lại DB, nên phải đo độ trễ đồng bộ và không tuyên bố kết quả đầy đủ khi index chưa bắt kịp.
- Kiểm tra payload index thực tế cho businessId, productId, canonical category, status, price và các thuộc tính thường lọc. Hiện chưa có bằng chứng từ DB đang chạy để khẳng định index nào thiếu. Việc tạo/đối soát index thuộc initializer V3, không chỉ “collection tồn tại thì bỏ qua”. Payload index là phần hỗ trợ filtered search theo [Qdrant indexing](https://qdrant.tech/documentation/manage-data/indexing/).

**4.2. Chuẩn hóa yêu cầu tìm kiếm**

- Mở rộng `ProductSearchRequestV3`: chế độ sắp xếp, category đích, ràng buộc bắt buộc, sở thích, điều kiện phủ định, ngân sách và nguồn tham chiếu. Toán tử chỉ cho phép theo schema: equals, any-of, exclude và range khi kiểu dữ liệu phù hợp.
- Mỗi điều kiện có nguồn: khách nói rõ, kế thừa từ nhu cầu đang tiếp diễn, hoặc AI đề xuất. Suy luận của AI không tự trở thành điều kiện bắt buộc.
- Tách “schema cho phép lọc chính xác” khỏi “khách bắt buộc thuộc tính này”. `IsStrict` không phải căn cứ duy nhất để quyết định mức bắt buộc. Thuộc tính không có dữ liệu xác minh phải trả trạng thái chưa xác minh, không coi similarity cao là bằng chứng đáp ứng.
- Category rộng như “quần” được ánh xạ tới tập category con hợp lệ trong catalogue. Không ép AI chọn đúng một loại quần hẹp khi khách chưa nói rõ.
- Chuẩn hóa đơn vị tiền, khoảng giá mâu thuẫn, màu/size đồng nghĩa, phủ định như “không màu vàng”. Không đưa khóa payload/tên field do AI tự tạo trực tiếp vào filter.
- Tenant và trạng thái hợp lệ do backend quyết định. Chính sách tồn kho phải dựa trên dữ liệu/chính sách bán hàng thực tế; không đồng nhất active với còn hàng, hoặc thiếu dữ liệu với hết hàng.

**4.3. Phân biệt các yêu cầu về giá**

| Câu hỏi | Cách xử lý dự kiến |
| --- | --- |
| “Sản phẩm rẻ”, “quần bình dân” | Áp dụng Low từ config; xếp theo phù hợp, không gọi kết quả là rẻ nhất |
| “Quần rẻ nhất” | Lọc toàn bộ tập category phù hợp, sắp giá tăng dần và tie-break ổn định |
| “Quần màu vàng đi dạ hội rẻ” | Category + màu bắt buộc + Low; mục đích dạ hội dùng semantic và bằng chứng sản phẩm |
| “Quần vàng đi dạ hội rẻ nhất” | Xác định tập đáp ứng điều kiện trước; chỉ khẳng định rẻ nhất nếu đã bao phủ tập đó, nếu không nói “rẻ nhất trong các mẫu tìm được” |
| “Rẻ hơn cái thứ hai” | Resolve đúng reference, đọc giá hiện tại, dùng DownSell và cửa sổ giá đang có |
| “Phối thêm áo và giày, tổng dưới 700k” | Ngân sách tổng cho nhóm bổ sung; không áp riêng 700k cho từng món |

Với truy vấn lọc có cấu trúc, ưu tiên đường database/index có thể đảm bảo phạm vi sắp giá. Không sort riêng top 40 semantic rồi tuyên bố cực trị toàn catalogue. Với ngân sách mơ hồ “cả bộ”, hỏi rõ có tính món nguồn hay không nếu điều đó làm thay đổi kết quả.

### 5. P1 — Tăng chất lượng retrieval và reranking

**5.1. Retrieval phù hợp loại truy vấn**

- ID/external SKU chính xác: lookup tenant-scoped trước; không cần embedding để xác định một ID đã biết. Tên gần giống không được giả làm ID chính xác.
- Duyệt category/giá: đường lọc và sắp xếp có cấu trúc, không bắt buộc embedding/reranker cho yêu cầu đơn giản.
- Nhu cầu tự nhiên: so sánh baseline hai dense hiện tại với dense + BM25, sau đó thử cả ba nhánh. Đo trước khi chọn; hai nhánh dense gần nhau không mặc nhiên tốt hơn một dense + lexical.
- Tạo sparse query bằng cùng cách chuẩn hóa/token hóa với sparse document. Kiểm tra truy vấn tiếng Việt có dấu/không dấu, mã model, tên brand, màu và lỗi gõ. BM25 text hiện chỉ name/brand/category nên muốn mở rộng thuộc tính phải có backfill/version tương ứng.
- RRF thường là bước đầu tương thích với server đang ghim 1.15.5. Không dùng weighted RRF native ngay: tài liệu ghi tính năng này từ 1.17; tùy chỉnh k từ 1.16. Nếu cần trọng số, đánh giá fusion ở ứng dụng hoặc đề xuất nâng server riêng. Nguồn: [Qdrant hybrid queries](https://qdrant.tech/documentation/search/hybrid-queries/).
- Candidate budget theo nhóm truy vấn và giới hạn vận hành V3; `TopKDocument` vẫn là giới hạn sản phẩm cuối. Có thể lấy thêm một đợt có giới hạn khi DB loại hàng stale, không loop tìm kiếm vô hạn.

**5.2. Representation và reranker**

- Đề xuất `SemanticEmbeddingV3.md` mô tả bản thân sản phẩm: loại, đối tượng, thuộc tính, mục đích/phong cách có căn cứ. Không nhồi cụm “mua”, câu hỏi SEO hoặc danh sách món mua kèm vào vector của sản phẩm nguồn.
- Giữ dữ liệu kỹ thuật, tên/mã và dữ liệu ngữ nghĩa có vai trò rõ ràng; giá/tồn kho là dữ liệu động để filter/đọc DB. Prompt mới chỉ có hiệu lực qua đường indexing V3 đã được phê duyệt; không tự đổi handler embed cũ.
- Chuẩn bị record rerank ngắn, ưu tiên tên/category và thuộc tính liên quan trước mô tả dài. Google quy định giới hạn title + content của record; model 004 có context 1024 token và phần vượt bị cắt. Giữ model version cố định; so sánh fast-004/default-004 trên dữ liệu tiếng Việt thực tế, không đổi sang latest chỉ vì mới. Nguồn: [Google Ranking API](https://docs.cloud.google.com/generative-ai-app-builder/docs/ranking).
- Ngưỡng Business config vẫn quyết định kết quả đạt chuẩn. Bỏ việc âm thầm coi fallback 0.25 như kết quả đạt; nếu hỗ trợ gợi ý gần đúng, trả riêng mức khớp và điều kiện chưa đáp ứng. Không gọi rerank score là xác suất đúng.
- Không tự nới ngân sách, tương thích kỹ thuật hay điều kiện khách bắt buộc. Chỉ thử nới sở thích mềm và thông báo rõ; thay đổi điều kiện bắt buộc cần khách đồng ý.
- Nếu reranker lỗi, phân biệt với không có hàng. Chỉ trả danh sách suy giảm chất lượng khi đã kiểm chứng điều kiện và đánh dấu rõ; không chế score hoặc dùng lỗi provider làm bằng chứng catalogue rỗng.

### 6. P2 — Cross-sell và up/down-sell có căn cứ

**Cross-sell không cần phụ thuộc cross-sell intent cũ hay danh sách mua chung có sẵn.** Khi khách nói “phối thêm gì”, AI chọn tool mua kèm từ lời hỏi và ngữ cảnh; backend không cần đợi một event/intent analytics cũ để cho phép chức năng chạy.

1. Resolve sản phẩm nguồn bằng `ProductReferenceV3`, thứ tự hiển thị và ngữ cảnh; hỏi lại nếu nhiều nguồn đều hợp lý. Đọc thông tin cần thiết hiện hành, không dựa giá/stock trong summary.
2. AI đề xuất tối đa ba target category từ danh sách hợp lệ, ưu tiên category shop có hàng. Ví dụ quần jean vintage -> áo thun, sneaker, áo khoác; nếu khách chỉ hỏi giày thì không thêm áo.
3. Service kiểm tra target, tạo query cho từng món đích, mang theo phong cách/dịp sử dụng liên quan. Không sao chép mọi thuộc tính của quần sang giày, cũng không nhét tên quần vào mọi query.
4. Tìm candidate theo từng target rồi kiểm tra **cặp nguồn–candidate**: vai trò bổ trợ, điều kiện khách, mục đích và thuộc tính liên quan. Phân biệt phối phong cách (gợi ý) với tương thích kỹ thuật (cần bằng chứng như chuẩn cổng/model/kích thước).
5. Xếp theo phù hợp nhu cầu và bằng chứng tương thích trước; đa dạng mẫu/nhóm sau. Không đề xuất món thay thế như phụ kiện, không dùng điểm semantic đơn lẻ để khẳng định “chắc chắn tương thích”.
6. Nếu ngân sách là tổng nhóm, chọn tổ hợp không vượt ngân sách bằng giá decimal hiện hành; giữ giới hạn TopK tổng và không nhân ngân sách cho mỗi nhóm. Nếu không đủ một bộ, trả bộ chưa đầy đủ và nói món nào thiếu.
7. Trả lý do có căn cứ và điểm chưa xác minh cho từng món; giữ xử lý partial success, loại trùng, loại sản phẩm nguồn đang có.

Logic lựa chọn/kiểm tra nằm trong service; có thể dùng rerank query gồm dữ kiện nguồn và nhu cầu để hỗ trợ, nhưng score reranker không thay thế kiểm tra kỹ thuật. Chỉ thêm lượt model chấm cặp nếu benchmark cho thấy đáng chi phí.

**Up/down-sell:** giữ công thức cửa sổ giá hiện tại. Bổ sung so sánh thuộc tính theo ưu tiên của khách; đắt hơn không tự động là nâng cấp, rẻ hơn không tự động là kém hơn. Không đủ bằng chứng thì gọi là “lựa chọn giá cao/thấp hơn”, không bịa lợi ích. Khi cửa sổ hiện tại không có hàng, báo rõ; mở rộng cửa sổ là thay đổi chính sách cần quyết định riêng.

### 7. P2 — Hội thoại, prompt và giới hạn thực thi

- Bổ sung trạng thái V3 có cấu trúc: nguồn đang nói tới, nhu cầu đang tiếp diễn, ngân sách/phạm vi ngân sách, điều kiện bắt buộc, sở thích, ID đã xem và điều kiện khách đã từ chối. Mỗi giá trị có nguồn và lượt cập nhật; tin nhắn mới thay thế phần xung đột, chuyển chủ đề thì không mang toàn bộ filter cũ sang.
- Cache trạng thái theo business/customer/conversation và có version/TTL; không sửa format lưu trữ legacy. Không tạo một kiểu product reference thứ hai để lưu state; dùng canonical ID và `ProductReferenceV3` tại ranh giới sản phẩm.
- Giữ danh sách category gọn trong prompt để AI chọn; chỉ đưa schema thuộc tính chi tiết của nhóm liên quan khi catalogue lớn. Service luôn validate trên schema đầy đủ. Category hợp lệ nhưng shop không có hàng phải được phân biệt với category không hợp lệ.
- Chỉ hydrate đầy đủ reference cần dùng; không đánh đổi việc xác thực tenant/giá hiện tại để giảm token. Cache schema/query embedding phải có khóa version và tenant khi nội dung phụ thuộc shop; dữ liệu giá/stock không lấy từ cache không kiểm chứng.
- Sửa các quy tắc prompt mâu thuẫn thành thứ tự: an toàn/tenant -> điều kiện bắt buộc -> bằng chứng -> mức khớp -> cách trình bày. Tool result có trạng thái Exact/Partial/NoMatch/Unavailable và các điều kiện chưa xác minh; Partial không có nghĩa được hiển thị hàng vi phạm điều kiện bắt buộc như một kết quả đúng.
- Xác thực `selectedProductIds`/`comparedProductIds` chỉ lấy từ tập sản phẩm hợp lệ; canonical ID dành cho backend, external ID dành cho link giỏ hàng. Giữ hợp đồng JSON và event cũ.
- Thêm giới hạn số tool calls, chống gọi trùng arguments, deadline/cancellation và retry có giới hạn cho lỗi tạm thời. Có thể dùng invocation filters của [Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/filters) để kiểm soát quá trình gọi tool tự động; kiểm tra API tương thích phiên bản package đang dùng khi triển khai.
- Ưu tiên batch embedding và batch DB reads. Không chạy song song trên cùng scoped DbContext. Chỉ song song các request độc lập với giới hạn concurrency rõ ràng.
- Theo dõi latency từng stage, số candidates, mức khớp, lỗi provider và config/index version; tránh log nguyên chat/PII không cần thiết. Số token quan sát thêm chỉ phục vụ vận hành, không thay cách đọc token tính phí trong Kernel/SendMessage.

### 8. P3 — Học từ dữ liệu mua thật khi đủ dữ liệu

- Có thể tận dụng `ConversationOrder`/`ConversationOrderEvent` sau khi kiểm chứng độ đầy đủ của dữ liệu. Map ExternalProductId về canonical ID trong đúng business, chống trùng order/event, phân biệt trạng thái thành công với hủy/hoàn.
- Xây tín hiệu đồng mua theo cặp có support tối thiểu, tránh món phổ biến xuất hiện trong mọi gợi ý; đánh giá trên giai đoạn thời gian chưa dùng để tính thống kê. Không áp các con số ngưỡng của nhà cung cấp khác máy móc vào hệ thống này.
- Đồng xem, đồng chat và đồng mua là tín hiệu khác nhau. AWS cũng tách use case frequently bought together dựa trên purchase khỏi also viewed: [Amazon Personalize ecommerce use cases](https://docs.aws.amazon.com/personalize/latest/dg/ECOMMERCE-use-cases.html). Đây là tham khảo thiết kế, không phải đề xuất chuyển sang AWS.
- Dùng đồng mua để bổ sung candidates/điểm ưu tiên sau kiểm tra tương thích, không ghi lẫn vào semantic embedding của sản phẩm. Với shop ít dữ liệu, tiếp tục cách category + thuộc tính + source–candidate ở P2.
- Không triển khai giai đoạn này trước khi chất lượng P0–P2 ổn định và dữ liệu đơn hàng đủ tin cậy.

### 9. File dự kiến tác động khi triển khai

| Nhóm | File/thành phần V3 |
| --- | --- |
| Logic sản phẩm | `IProductSemanticSearchByAI.cs`, `ProductSemanticSearchByAI.cs`, `ProductPluginV3.cs`, `ProductSearchRequestV3.cs` |
| Kết quả có giải thích | Có thể thêm `ProductSearchOutcomeV3.cs`; chứa `ProductReferenceV3`, không thay thế nó |
| Context và orchestration | `ConversationContextCacheV3.cs`, `ConversationContextServiceV3.cs`, `KernelChatServiceV3.cs`; thêm `ProductToolInvocationFilterV3.cs` khi cần |
| Prompt | `SemanticKernelSystemV3.md`; `SemanticEmbeddingV3.md` nếu triển khai đường indexing V3 |
| Adapter hạ tầng | `QdrantProductRetrievalV3.cs`, `ProductRerankerV3.cs` chỉ giao tiếp provider; initializer/sync worker V3 chỉ sau khi duyệt phương án index |
| Cấu hình vận hành | `ProductSearchOptionsV3.cs` cho timeout/candidate budget/feature flag; không thay Business config hay ghi đè các giới hạn hiện có |
| Đánh giá | `SearchEvaluationCasesV3.json` và `SearchEvaluationReportV3.md` khi thực hiện benchmark |

Đây là danh sách dự kiến, không tạo sẵn hàng loạt class trống. SendMessageV3 chỉ nhận dữ liệu AI/reference đã chuẩn hóa; các đoạn tính toán, transaction và publish phải được so sánh lại với nguyên mẫu trước bàn giao. Chỉ sửa wiring tối thiểu tại các điểm đã được cho phép.

### 10. Thứ tự bàn giao và tiêu chí chấp nhận

1. **Đợt 1 — Baseline + P0:** chốt dữ liệu/index, ràng buộc và ý nghĩa giá. Bộ dữ liệu khởi đầu đề xuất 150–300 câu tiếng Việt và 30–50 hội thoại nhiều lượt; tách tập chỉnh tham số và tập đánh giá cuối. Các con số này là quy mô khởi đầu, chưa phải dữ liệu đã thu thập.
2. **Đợt 2 — P1:** thử riêng lexical, dense, fusion và reranking trên cùng dữ liệu; chỉ giữ thay đổi cải thiện chất lượng trong ngân sách latency/chi phí đã chốt. Không thay nhiều thành phần một lúc rồi không biết phần nào có ích.
3. **Đợt 3 — P2:** source-aware cross-sell, up/down-sell, state và tool guards; kiểm tra hồi quy tìm kiếm đơn giản và tham chiếu “cái đầu/cái thứ hai/mẫu khác”.
4. **Đợt 4 — P3:** thêm tín hiệu mua chung nếu dữ liệu đủ; triển khai theo feature flag, đối chiếu một nhóm shop trước rồi mở rộng. Không tự đưa lên production trong nhiệm vụ lập plan.

Đo Recall@candidate để biết bước tìm có bỏ sót, nDCG@K để biết thứ tự có tốt, cùng độ trễ p50/p95 và chi phí mỗi lượt. Đây là các hướng đánh giá được mô tả trong [Qdrant evaluating search pipelines](https://qdrant.tech/course/multi-vector-search/module-3/evaluating-pipelines/); chất lượng thực tế phải đo trên catalogue của Mahiru.

Các gate bổ sung riêng cho dự án:

- Không có lỗi khác tenant/ID sai/vượt ngân sách bắt buộc trong bộ kiểm tra; phát hiện một lỗi như vậy thì chưa mở rollout.
- “Rẻ nhất” đúng với phạm vi đã công bố; no-match khác provider-error; không có lời khẳng định tương thích thiếu bằng chứng.
- Cross-sell đúng vai trò bổ trợ, đúng nguồn, không trùng, không vượt TopK/ngân sách; nhóm lỗi không làm mất nhóm thành công.
- Kiểm tra category sau sửa giá, đổi schema, re-embed, xóa sản phẩm và đồng bộ trễ. Bản index chưa đạt độ phủ/độ mới không được thay bản hiện tại.
- Rerank/fusion mới tốt hơn baseline trên tập giữ lại hoặc có lợi ích latency/chi phí rõ ràng với mức giảm chất lượng được chấp thuận; chưa đặt ngưỡng phần trăm tùy ý khi chưa có baseline.
- Billing, token extraction, business config, DB commit và publish event giữ nguyên; clean build thành công. Benchmark/API cần môi trường và quyền chạy riêng, không thay thế bằng kết quả build.

**Khuyến nghị thực hiện trước:** P0 rồi hybrid + reranking ở P1, sau đó kiểm tra cặp sản phẩm của cross-sell ở P2. Chưa ưu tiên thêm agent, model mới hoặc hệ thống học đồng mua khi các lớp nền chưa được đo và ổn định.
