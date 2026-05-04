namespace AutoTest.Domain.Common.Enums;

// Lifecycle states are 1-based so that the C# default of 0 means "unset" —
// any explicitly assigned value is preserved by EF (no HasSentinel needed).
// "Soft-delete" = setting Status to Inactive (row stays in DB but disappears
// from exam / practice / marathon queries). Hard-delete is a separate explicit
// admin action via PermanentDeleteQuestionCommand.
public enum QuestionStatus
{
    // Hidden from users (awaiting admin review, failed auto-categorization,
    // soft-deleted by admin, or collapsed by the dedup pass).
    Inactive = 1,

    // Visible to users — appears in exam pools, practice, marathon, ticket modes.
    Active = 2
}
