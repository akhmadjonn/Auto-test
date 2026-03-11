# Backend Agent Instructions — Avtolider .NET 8 API

Read the root CLAUDE.md first.

## Your Scope
You own everything in backend/. MUST NOT modify frontend/ or infrastructure/. MAY read contracts/openapi.yaml.

## NuGet Packages
- MediatR 12.x, FluentValidation.DependencyInjectionExtensions 11.x, Mapster 7.x
- Npgsql.EntityFrameworkCore.PostgreSQL 8.x, EFCore.NamingConventions 8.x, StackExchange.Redis 2.x
- Microsoft.AspNetCore.Authentication.JwtBearer 8.x, Telegram.Bot.Extensions.LoginWidget 1.x
- AWSSDK.S3 3.x (MinIO uses S3 protocol)
- ClosedXML 0.104.x (Excel parsing for bulk import + export)
- SixLabors.ImageSharp 3.x (image resize, WebP conversion, thumbnail generation)
- AspNetCore.HealthChecks.NpgSql 8.x, AspNetCore.HealthChecks.Redis 8.x
- Serilog.AspNetCore 8.x

## Domain Entities
User, Question, AnswerOption, Category, Tag, ExamTemplate, ExamPoolRule, ExamSession, SessionQuestion, UserQuestionState, UserCategoryStat, Subscription, SubscriptionPlan, PaymentTransaction, OtpRequest, BaseAuditableEntity (Id, CreatedAt, UpdatedAt),
AuditLog (Id, UserId, Action, EntityType, EntityId, OldValues JSONB, NewValues JSONB, IpAddress, CreatedAt),
SystemSetting (Key string PK, Value string, Description, UpdatedBy, UpdatedAt),
Announcement (Id, Title LocalizedText, Content LocalizedText, Type enum, IsActive, StartsAt, ExpiresAt, CreatedBy, CreatedAt)

## Enums
ExamStatus (InProgress/Completed/Expired), ExamMode (Exam/Ticket/Marathon), SubscriptionStatus (None/Active/Expired/Cancelled), PaymentProvider (Payme/Click), PaymentStatus (Pending/Completed/Failed/Refunded), Difficulty (1/2/3), LicenseCategory (AB/CD), Language (Uz/UzLatin/Ru), AuthProvider (Phone/Telegram), AuditAction (Create/Update/Delete/StatusChange/Login/Export), AnnouncementType (Info/Warning/Important)

## Value Objects
- LocalizedText: { Uz: string, UzLatin: string, Ru: string } — TRILINGUAL, mapped to JSONB via OwnsOne().ToJson()
   - Uz = Uzbek Cyrillic (e.g., "Қайси белги")
   - UzLatin = Uzbek Latin (e.g., "Qaysi belgi")
   - Ru = Russian (e.g., "Какой знак")
   - Get(Language lang) method returns correct variant
- Money: { Amount: long, Currency: string } — amounts always in tiyins

## Application Layer Structure (CQRS)
Features/Auth/ — SendOtpCommand, VerifyOtpCommand, TelegramLoginCommand, RefreshTokenCommand, LogoutCommand, GetCurrentUserQuery
Features/Questions/ — CreateQuestionCommand, UpdateQuestionCommand, DeleteQuestionCommand, BulkImportQuestionsCommand, GetQuestionByIdQuery, GetQuestionsByCategoryQuery, GetQuestionsForPracticeQuery, GetRandomQuestionsQuery, GetTicketsListQuery, GetQuestionsByTicketQuery
Features/Categories/ — CreateCategoryCommand, UpdateCategoryCommand, GetCategoriesTreeQuery
Features/Exams/ — StartExamCommand, SubmitAnswerCommand, CompleteExamCommand, StartTicketExamCommand, StartMarathonCommand, ExpireStaleSessionsCommand, GetExamSessionQuery, GetExamResultQuery, GetExamHistoryQuery
Features/Practice/ — SubmitPracticeAnswerCommand, GetPracticeSessionQuery, GetDueReviewCountQuery
Features/Progress/ — GetUserDashboardQuery, GetCategoryPerformanceQuery
Features/Subscriptions/ — CreateSubscriptionCommand, CancelSubscriptionCommand, ProcessRecurringBillingCommand, GetSubscriptionStatusQuery, GetPlansQuery
Features/Payments/ — InitiatePaymentCommand, HandlePaymeWebhookCommand, HandleClickWebhookCommand
Features/Admin/ — SeedQuestionsCommand, BulkImportQuestionsCommand (Excel+ZIP), ExportExcelTemplateQuery, ToggleQuestionStatusCommand, BulkToggleStatusCommand, PermanentDeleteQuestionCommand, CreatePlanCommand, UpdatePlanCommand, GetPaymentTransactionsQuery, GetRevenueReportQuery, ExportRevenueReportCommand, CreateExamTemplateCommand, UpdateExamTemplateCommand, GetExamTemplatesQuery, ExportUsersReportCommand, ExportExamStatsReportCommand, GetAuditLogsQuery, GetSystemSettingsQuery, UpdateSystemSettingCommand, GetUserDetailQuery, CreateAnnouncementCommand, UpdateAnnouncementCommand, DeleteAnnouncementCommand, GetAnnouncementsQuery, AdminDashboardQuery

## Application Interfaces
IApplicationDbContext, ICurrentUser, IFileStorageService, ISmsService, IPaymentProvider, ICacheService, IDateTimeProvider, IQuestionImportService, IImageProcessingService, IAuditLogService, IExcelExportService

## Infrastructure Services
- EskizSmsService: HTTP client for Eskiz.uz (auth email/password → JWT, POST /message/sms/send)
- JwtTokenService: access 15min + refresh 30d (Redis), rotation on use
- OtpService: 6-digit, HMAC-SHA256 hash in Redis, TTL 5min, rate limit 3/15min
- PaymePaymentProvider: Subscribe API (cards.create → verify → receipts.create → receipts.pay), test URL checkout.test.paycom.uz/api
- ClickPaymentProvider: Card Token API (card_token/create → verify → payment)
- PaymeWebhookHandler: JSON-RPC 2.0 (CheckPerformTransaction, CreateTransaction, PerformTransaction, CancelTransaction, CheckTransaction, GetStatement), auth HTTP Basic Paycom:KEY
- ClickWebhookHandler: REST two-stage (Prepare action=0, Complete action=1)
- MinioFileStorageService: S3 SDK — upload, presigned URLs (1h expiry), delete, bulk-delete. Keys: questions/{category}/{guid}.webp
- RedisCacheService: wrapping StackExchange.Redis
- ExcelQuestionParser (IQuestionImportService): parse .xlsx via ClosedXML, validate rows, return List<ImportQuestionDto> + List<ImportRowError>
- QuestionImageProcessor (IImageProcessingService): validate image type (magic bytes), resize if >1200px, convert to WebP via ImageSharp, generate 200x200 thumbnail
- AuditLogService: auto-log admin actions via MediatR post-processor, captures before/after JSON snapshots
- ExcelExportService: generate .xlsx reports using ClosedXML (users, revenue, exam stats, question performance)
- SystemSettingsService: load from DB on startup, cache in Redis, invalidate via Pub/Sub on change
- UzbekTransliterator: convert Uzbek Cyrillic ↔ Latin for search and data migration

## Background Services
- SessionExpirationService: every 30s, expire sessions past expires_at
- SubscriptionBillingService: daily 2AM, charge due subscriptions via saved card tokens
- EskizTokenRefreshService: refresh Eskiz JWT before expiry

## Exam & Practice Logic
StartExam: validate subscription/free-tier (read free_daily_exam_limit from SystemSettings) → check rate limit → load template + pool rules → SELECT random questions ORDER BY RANDOM() → shuffle → create session with expires_at → return WITHOUT correct answers
StartTicketExam: load specific ticket's 20 fixed questions → create session with timer → same flow
StartMarathon: load ALL questions in order → create session WITHOUT timer → save progress every 10 answers → can resume
SubmitAnswer: validate session active + not expired → store answer → NO feedback in exam mode
CompleteExam: calculate score server-side → update spaced repetition states → return WITH correct answers + explanations

## Leitner Spaced Repetition
5 boxes, intervals: [1, 2, 4, 8, 16] days. Correct → advance (max 5). Incorrect → reset to 1. Practice selection: 60% due reviews + 30% new + 10% weak categories.

## API Controllers
AuthController, CategoriesController, QuestionsController (+ tickets endpoints), ExamsController (+ start-ticket, start-marathon), PracticeController, ProgressController, SubscriptionsController, PaymentsController, AnnouncementsController (public)
Admin: AdminQuestionsController, AdminCategoriesController, AdminPlansController, AdminPaymentsController, AdminExamTemplatesController, AdminReportsController, AdminAuditLogController, AdminSettingsController, AdminUsersController, AdminAnnouncementsController, AdminDashboardController

## Question Image Handling (CRITICAL)
- AnswerOption entity MUST have nullable ImageUrl field (string?)
- Four image scenarios: A) text-only, B) question-image + text-answers, C) text-question + image-answers, D) both
- Admin endpoints use multipart/form-data (NOT JSON)
- Images in MinIO: questions/{categorySlug}/{guid}.webp (auto-converted)
- Returned as presigned URLs (1h expiry)
- Thumbnails: {guid}_thumb.webp (200x200)
- Soft delete = is_active=false. Hard delete = remove images + cascade

## IFileStorageService Interface
UploadQuestionImageAsync, UploadAnswerOptionImageAsync, GetPresignedUrlAsync, GetThumbnailUrlAsync, DeleteAsync, DeleteManyAsync

## Database Config
Schema: "autotest", UseSnakeCaseNamingConvention(), JSONB for LocalizedText (3 fields: uz, uz_latin, ru)

## Coding Standards
- Every handler has FluentValidation validator
- Every handler returns ApiResponse<T>
- Structured logging
- CancellationToken in every async method
- Redis keys: avtolider:{entity}:{id}
- SystemSettings reads from cache, not hardcoded values
