namespace Jobranger.Messaging.Learning;

public sealed record LearningExample(
    long DecisionId,
    string AdId,
    string Company,
    string Title,
    string Decision,
    string At);
