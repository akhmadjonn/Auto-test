namespace AutoTest.Domain.Common.Enums;

public enum ExamStatus
{
    InProgress = 0,
    Completed = 1,
    Expired = 2,
    Abandoned = 3,
    // Paused: timer is stopped, ExpiresAt is null, RemainingSecondsAtPause holds the
    // saved countdown. User has to call ResumeExam to continue. Only meaningful for
    // timed modes (Exam / Ticket / SpeedChallenge); marafon never enters this state.
    Paused = 4
}
