namespace GymOS.Domain.Experience;

/// <summary>The metrics a member can set a personal record on, per exercise. Kept to the three that
/// matter for a lift and are unambiguous from a logged session; append new values at the end to keep
/// stored integer values stable.</summary>
public enum PersonalRecordType
{
    MaxWeight,
    EstimatedOneRepMax,
    SessionVolume
}
