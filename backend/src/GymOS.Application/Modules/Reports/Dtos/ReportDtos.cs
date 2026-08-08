namespace GymOS.Application.Modules.Reports.Dtos;

public record RevenueReportPointDto(string Period, decimal Revenue);

public record AttendanceReportPointDto(DateOnly Date, int CheckIns);

public record MembershipBreakdownDto(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPlanType);

public record TrainerCommissionReportRowDto(string TrainerName, decimal TotalPending, decimal TotalPaid, int RecordCount);

public record EquipmentDowntimeReportRowDto(
    string AssetName, string AssetTag, int Incidents, double TotalDowntimeHours, decimal TotalMaintenanceCost);

public record InventoryStockMovementReportRowDto(
    string ItemName, string Sku, int TotalIn, int TotalOut, int NetChange, int CurrentQuantityOnHand);

public record CrmPipelineConversionReportDto(
    IReadOnlyDictionary<string, int> ByStage, int TotalLeads, int ConvertedCount, decimal ConversionRatePercent);

public record WorkoutActivityReportRowDto(
    string ExerciseName, string? MuscleGroup, int TimesLogged, int TotalSets, int TotalReps, decimal? AvgWeightKg);

public record NutritionFoodItemRowDto(string FoodItemName, int TimesLogged, decimal TotalCaloriesLogged);

public record NutritionReportDto(
    List<NutritionFoodItemRowDto> TopFoodItems, int TotalMealEntriesLogged, decimal TotalCaloriesLogged,
    int TotalWaterLogsLogged, int TotalWaterMlLogged);

public record AtRiskMemberRowDto(
    Guid MemberId, string FullName, string MemberCode, DateOnly LastCheckInDate, int DaysSinceLastVisit);

public record CohortRetentionPointDto(
    string CohortMonth, int CohortSize, int StillActiveCount, double RetentionRatePercent);

public record LtvBySourceRowDto(
    string Source, int MemberCount, decimal TotalRevenue, decimal AverageLtv);

/// <summary>One week of capture figures.</summary>
public record CaptureRatePointDto(
    DateOnly WeekStart, int VisitDays, int LoggedVisitDays, int OrphanLogDays, int CaptureRatePercent);

/// <summary>
/// How much of what happens in the gym the app captures. See GetLoggingCaptureReportQuery for why
/// this is the number the member-experience work is measured against.
/// </summary>
/// <param name="IsReliable">False when too many workouts were logged on days with no visit, which
/// means the rate has stopped describing gym behaviour (see CaptureRatePolicy).</param>
/// <param name="MembersVisitingWithoutLogging">Members who turned up but never recorded anything —
/// the population one-tap logging is aimed at.</param>
public record LoggingCaptureReportDto(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int TotalVisitDays,
    int TotalLoggedVisitDays,
    int TotalOrphanLogDays,
    int CaptureRatePercent,
    bool IsReliable,
    int MembersWhoVisited,
    int MembersWhoLogged,
    int MembersVisitingWithoutLogging,
    IReadOnlyList<CaptureRatePointDto> Weekly,
    int? MedianMinutesToLog,
    IReadOnlyList<LogLatencyBucketDto> LatencyBuckets,
    double? SessionsPerMemberPerWeek);

/// <summary>How many timed records fell into one latency bucket. See TimeToLogPolicy.</summary>
/// <param name="Bucket">Machine name of the bucket, e.g. "WithinTheHour".</param>
public record LogLatencyBucketDto(string Bucket, int Sessions);

/// <summary>One week-N return figure.</summary>
/// <param name="EligibleMembers">Members who joined long enough ago for this week to have finished.
/// Members still inside their week N are in neither this nor <paramref name="ReturnedMembers"/> —
/// they have not failed to return, their answer is not in yet. See ReturnRatePolicy.</param>
public record ReturnRatePointDto(int WeekNumber, int EligibleMembers, int ReturnedMembers, int RatePercent);

/// <summary>
/// Week-N return: the outcome the other gate metrics are proxies for. See GetReturnRateReportQuery.
/// </summary>
public record ReturnRateReportDto(DateOnly AsOf, IReadOnlyList<ReturnRatePointDto> Weeks);
