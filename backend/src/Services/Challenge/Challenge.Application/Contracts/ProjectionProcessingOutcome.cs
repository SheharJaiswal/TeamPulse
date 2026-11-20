namespace Challenge.Application.Contracts;

public enum ProjectionProcessingOutcome
{
    Applied,
    Stale,
    Duplicate,
    DuplicateEventIntegrityViolation,
    Invalid,
    UnsupportedSchemaVersion,
    InvalidProducer
}
