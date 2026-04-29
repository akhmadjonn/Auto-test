namespace AutoTest.Domain.Enum;

// Negative integer codes grouped by entity in blocks of 10.
// Naming: {Entity}{Action}Error for failures, {Entity}NotFound for 404s.
public enum ErrorEnum
{
    // General errors (-32000 to -32009)
    ErrorWhileProcess = -32000,
    ServiceTemporaryUnavailable = -32001,
    InternalServerError = -32002,
    ArgumentError = -32003,
    InvalidPayloadFormat = -32004,
    AlreadyExists = -32005,
    SaveDataError = -32006,
    Unauthorized = -32007,
    Forbidden = -32008,
    ValidationError = -32009,

    // User errors (-32010 to -32019)
    UserNotFound = -32010,
    AddUserError = -32011,
    UpdateUserError = -32012,
    GetUserError = -32013,
    DeleteUserError = -32014,
    UserBlocked = -32015,
    UserAlreadyExists = -32016,
    InvalidUserRole = -32017,

    // Auth / OTP errors (-32020 to -32029)
    OtpInvalid = -32020,
    OtpExpired = -32021,
    OtpRateLimited = -32022,
    OtpCooldown = -32023,
    OtpTooManyAttempts = -32024,
    InvalidToken = -32025,
    RefreshTokenExpired = -32026,
    ConcurrentAuthRequest = -32027,
    TelegramAuthError = -32028,
    SendOtpError = -32029,

    // Question errors (-32030 to -32039)
    QuestionNotFound = -32030,
    AddQuestionError = -32031,
    UpdateQuestionError = -32032,
    GetQuestionError = -32033,
    DeleteQuestionError = -32034,
    QuestionImportError = -32035,
    InvalidQuestionPayload = -32036,

    // AnswerOption errors (-32040 to -32049)
    AnswerOptionNotFound = -32040,
    AddAnswerOptionError = -32041,
    UpdateAnswerOptionError = -32042,
    DeleteAnswerOptionError = -32043,

    // Category errors (-32050 to -32059)
    CategoryNotFound = -32050,
    AddCategoryError = -32051,
    UpdateCategoryError = -32052,
    GetCategoryError = -32053,
    DeleteCategoryError = -32054,
    CategoryAlreadyExists = -32055,

    // Exam / Session errors (-32060 to -32079)
    ExamSessionNotFound = -32060,
    StartExamError = -32061,
    SubmitAnswerError = -32062,
    CompleteExamError = -32063,
    ExamSessionExpired = -32064,
    ExamSessionAlreadyCompleted = -32065,
    InvalidExamQuestion = -32066,
    ExamTemplateNotFound = -32067,
    AddExamTemplateError = -32068,
    UpdateExamTemplateError = -32069,
    DeleteExamTemplateError = -32070,
    ExamPoolRuleNotFound = -32071,

    // Practice errors (-32080 to -32089)
    PracticeSessionNotFound = -32080,
    GetPracticeSessionError = -32081,
    SubmitPracticeAnswerError = -32082,
    NoQuestionsForReview = -32083,

    // Subscription / Plan errors (-32090 to -32109)
    SubscriptionNotFound = -32090,
    CreateSubscriptionError = -32091,
    CancelSubscriptionError = -32092,
    GetSubscriptionError = -32093,
    SubscriptionExpired = -32094,
    SubscriptionAlreadyActive = -32095,
    SubscriptionPlanNotFound = -32100,
    AddSubscriptionPlanError = -32101,
    UpdateSubscriptionPlanError = -32102,
    DeleteSubscriptionPlanError = -32103,

    // Payment errors (-32110 to -32119)
    PaymentNotFound = -32110,
    InitiatePaymentError = -32111,
    PaymentWebhookError = -32112,
    PaymentAlreadyProcessed = -32113,
    PaymentFailed = -32114,
    InvalidPaymentAmount = -32115,

    // Announcement errors (-32120 to -32129)
    AnnouncementNotFound = -32120,
    AddAnnouncementError = -32121,
    UpdateAnnouncementError = -32122,
    GetAnnouncementError = -32123,
    DeleteAnnouncementError = -32124,

    // RoadSign / RoadSignCategory errors (-32130 to -32149)
    RoadSignNotFound = -32130,
    AddRoadSignError = -32131,
    UpdateRoadSignError = -32132,
    GetRoadSignError = -32133,
    DeleteRoadSignError = -32134,
    RoadSignCategoryNotFound = -32140,
    AddRoadSignCategoryError = -32141,
    UpdateRoadSignCategoryError = -32142,
    DeleteRoadSignCategoryError = -32143,

    // RoadMarking errors (-32150 to -32159)
    RoadMarkingNotFound = -32150,
    AddRoadMarkingError = -32151,
    UpdateRoadMarkingError = -32152,
    GetRoadMarkingError = -32153,
    DeleteRoadMarkingError = -32154,

    // HazardLabel errors (-32160 to -32169)
    HazardLabelNotFound = -32160,
    AddHazardLabelError = -32161,
    UpdateHazardLabelError = -32162,
    GetHazardLabelError = -32163,
    DeleteHazardLabelError = -32164,

    // TrafficFine errors (-32170 to -32179)
    TrafficFineNotFound = -32170,
    AddTrafficFineError = -32171,
    UpdateTrafficFineError = -32172,
    GetTrafficFineError = -32173,
    DeleteTrafficFineError = -32174,

    // FirstAid errors (-32180 to -32199)
    FirstAidProcedureNotFound = -32180,
    AddFirstAidProcedureError = -32181,
    UpdateFirstAidProcedureError = -32182,
    DeleteFirstAidProcedureError = -32183,
    FirstAidStepNotFound = -32190,
    AddFirstAidStepError = -32191,
    UpdateFirstAidStepError = -32192,
    DeleteFirstAidStepError = -32193,

    // Glossary errors (-32200 to -32219)
    GlossaryTermNotFound = -32200,
    AddGlossaryTermError = -32201,
    UpdateGlossaryTermError = -32202,
    DeleteGlossaryTermError = -32203,
    GlossaryCategoryNotFound = -32210,
    AddGlossaryCategoryError = -32211,
    UpdateGlossaryCategoryError = -32212,
    DeleteGlossaryCategoryError = -32213,

    // ColorVisionPlate errors (-32220 to -32229)
    ColorVisionPlateNotFound = -32220,
    AddColorVisionPlateError = -32221,
    UpdateColorVisionPlateError = -32222,
    DeleteColorVisionPlateError = -32223,

    // VideoLesson / VideoCategory errors (-32230 to -32249)
    VideoLessonNotFound = -32230,
    AddVideoLessonError = -32231,
    UpdateVideoLessonError = -32232,
    GetVideoLessonError = -32233,
    DeleteVideoLessonError = -32234,
    VideoCategoryNotFound = -32240,
    AddVideoCategoryError = -32241,
    UpdateVideoCategoryError = -32242,
    DeleteVideoCategoryError = -32243,

    // Favorite errors (-32250 to -32259)
    FavoriteNotFound = -32250,
    AddFavoriteError = -32251,
    RemoveFavoriteError = -32252,
    GetFavoritesError = -32253,

    // Settings errors (-32260 to -32269)
    SettingNotFound = -32260,
    UpdateSettingError = -32261,
    GetSettingError = -32262,

    // Leaderboard errors (-32270 to -32279)
    GetLeaderboardError = -32270,
    LeaderboardSnapshotNotFound = -32271,

    // Progress errors (-32280 to -32289)
    GetProgressError = -32280,
    UserCategoryStatNotFound = -32281,

    // AuditLog errors (-32290 to -32299)
    AuditLogNotFound = -32290,
    GetAuditLogError = -32291,

    // Tag errors (-32300 to -32309)
    TagNotFound = -32300,
    AddTagError = -32301,
    UpdateTagError = -32302,
    DeleteTagError = -32303,
}
