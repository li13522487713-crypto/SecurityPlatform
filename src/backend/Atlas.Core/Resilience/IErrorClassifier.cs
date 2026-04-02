namespace Atlas.Core.Resilience;

public interface IErrorClassifier
{
    ClassifiedError Classify(Exception exception);
}
