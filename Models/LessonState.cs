using System;

namespace Chess.Models;

public enum FoundationsLessonStep
{
    Welcome,
    IntroductionVisible,
    TimeVisible,
    IncrementVisible,
    InitialPositionVisible
}

public static class LearningProgressKeys
{
    public const string SchemaVersion = "learning.schema_version";
    public const string FoundationsStep = "foundations.step";
    public const string FoundationsStarted = "foundations.started";
    public const string FoundationsCompletedLessons = "foundations.completed_lessons";
    public const string CurrentFoundationsLesson = "foundations.current_lesson";
    public const string FoundationsCourse = "course.foundations";
    public const string FoundationsLessonId = "foundations_1";
    public const string CurrentSchemaVersion = "1";
}

public static class LearningProgress
{
    public static FoundationsLessonStep GetFoundationsStep(System.Collections.Generic.IReadOnlyDictionary<string, string> progression)
    {
        return progression.TryGetValue(LearningProgressKeys.FoundationsStep, out var value)
            && Enum.TryParse<FoundationsLessonStep>(value, out var step)
            ? step
            : FoundationsLessonStep.Welcome;
    }
}
