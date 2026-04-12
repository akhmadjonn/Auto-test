namespace AutoTest.Infrastructure.Persistence.Constants;

public static class TableNames
{
    public const string Users = nameof(Users);
    public const string Categories = nameof(Categories);
    public const string Questions = nameof(Questions);
    public const string AnswerOptions = nameof(AnswerOptions);
    public const string Tags = nameof(Tags);
    public const string QuestionTags = nameof(QuestionTags);
    public const string ExamTemplates = nameof(ExamTemplates);
    public const string ExamPoolRules = nameof(ExamPoolRules);
    public const string ExamSessions = nameof(ExamSessions);
    public const string SessionQuestions = nameof(SessionQuestions);
    public const string UserQuestionStates = nameof(UserQuestionStates);
    public const string UserCategoryStats = nameof(UserCategoryStats);
    public const string SubscriptionPlans = nameof(SubscriptionPlans);
    public const string Subscriptions = nameof(Subscriptions);
    public const string PaymentTransactions = nameof(PaymentTransactions);
    public const string OtpRequests = nameof(OtpRequests);
    public const string AuditLogs = nameof(AuditLogs);
    public const string SystemSettings = nameof(SystemSettings);
    public const string Announcements = nameof(Announcements);
    public const string UserDevices = nameof(UserDevices);

    // Phase 2 — Engagement
    public const string UserDailyStats = nameof(UserDailyStats);
    public const string UserFavoriteQuestions = nameof(UserFavoriteQuestions);
    public const string UserSettings = nameof(UserSettings);
    public const string LeaderboardSnapshots = nameof(LeaderboardSnapshots);

    // Phase 2 — Reference content
    public const string TrafficFines = nameof(TrafficFines);
    public const string HazardLabels = nameof(HazardLabels);
    public const string FirstAidProcedures = nameof(FirstAidProcedures);
    public const string FirstAidSteps = nameof(FirstAidSteps);
    public const string GlossaryCategories = nameof(GlossaryCategories);
    public const string GlossaryTerms = nameof(GlossaryTerms);
    public const string ColorVisionPlates = nameof(ColorVisionPlates);

    // Phase 3 — Road Signs & Markings
    public const string RoadSignCategories = nameof(RoadSignCategories);
    public const string RoadSigns = nameof(RoadSigns);
    public const string RoadMarkings = nameof(RoadMarkings);
}
