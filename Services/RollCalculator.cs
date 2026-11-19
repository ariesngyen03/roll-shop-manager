using RollShopManager.Models;

namespace RollShopManager.Services;

/// <summary>
/// Các công thức tính toán thuần (không truy cập DB) cho vòng đời trục. Tách riêng để dễ kiểm thử đơn vị.
/// </summary>
public static class RollCalculator
{
    public const int PercentDecimals = 2;
    public const int RatioDecimals = 6;

    /// <summary>Sắp xếp chuỗi nhật ký mài theo GrindDate ASC, Id ASC (thứ tự chuẩn để nối đuôi ĐK).</summary>
    public static List<GrindingLogEntity> OrderChain(IEnumerable<GrindingLogEntity> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        return logs.OrderBy(log => log.GrindDate).ThenBy(log => log.Id).ToList();
    }

    public static decimal CalculateRemainingUsableMm(decimal currentDiameter, decimal minDiameter)
    {
        return Math.Max(0m, currentDiameter - minDiameter);
    }

    /// <summary>% tuổi thọ còn lại = (Current - Min) / (Max - Min) * 100, giới hạn trong [0, 100].</summary>
    public static decimal CalculateRemainingLifetimePercent(decimal currentDiameter, decimal maxDiameter, decimal minDiameter)
    {
        decimal usableLayer = maxDiameter - minDiameter;
        if (usableLayer <= 0m)
        {
            return 0m;
        }

        decimal percent = (currentDiameter - minDiameter) / usableLayer * 100m;
        percent = Math.Clamp(percent, 0m, 100m);
        return Math.Round(percent, PercentDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Tiêu hao trung bình mỗi lần mài dựa trên các ca mài thực tế đã ghi nhận trong hệ thống.
    /// Nếu trục chưa có ca mài nào được ghi nhận (loggedGrindCount == 0) thì trả về 0 (chưa đủ dữ liệu để ước tính).
    /// </summary>
    public static decimal CalculateAverageWearPerGrind(decimal loggedWearMm, int loggedGrindCount, string rollType, out bool isDefaultRate)
    {
        if (loggedGrindCount > 0 && loggedWearMm > 0m)
        {
            isDefaultRate = false;
            return loggedWearMm / loggedGrindCount;
        }

        isDefaultRate = true;
        return 0m;
    }

    /// <summary>
    /// Ước tính số lần mài còn lại = Floor(RemainingUsableMm / AverageWearPerGrind).
    /// Chỉ tính khi đã có ít nhất 1 ca mài thực tế (averageWearPerGrind &gt; 0).
    /// </summary>
    public static int CalculateEstimatedRemainingGrinds(decimal remainingUsableMm, decimal averageWearPerGrind)
    {
        if (remainingUsableMm <= 0m || averageWearPerGrind <= 0m)
        {
            return 0;
        }

        decimal grinds = Math.Floor(remainingUsableMm / averageWearPerGrind);
        return grinds >= int.MaxValue ? int.MaxValue : (int)grinds;
    }

    public static string DetermineStatus(decimal currentDiameter, decimal minDiameter, bool lastGrindIsAbnormal)
    {
        if (currentDiameter <= minDiameter)
        {
            return RollStatusTexts.Scrap;
        }

        return lastGrindIsAbnormal ? RollStatusTexts.NeedInspection : RollStatusTexts.InUse;
    }

    /// <summary>Dựng thông tin vòng đời trục từ dữ liệu khai báo (bắt đầu từ InitialDiameter = ĐK hiện trạng) và chuỗi nhật ký mài đã sắp xếp.</summary>
    public static RollLifetimeViewDto BuildLifetime(RollEntity roll, IReadOnlyList<GrindingLogEntity> orderedLogs)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(orderedLogs);

        GrindingLogEntity? latest = orderedLogs.Count > 0 ? orderedLogs[^1] : null;
        decimal currentDiameter = latest?.PostGrindDiameter ?? roll.InitialDiameter;
        int loggedGrindCount = orderedLogs.Count;
        int totalGrindCount = roll.InitialGrindCount + loggedGrindCount;
        decimal totalWearMm = Math.Max(0m, roll.MaxDiameter - currentDiameter);
        decimal loggedWearMm = Math.Max(0m, roll.InitialDiameter - currentDiameter);
        decimal remainingUsableMm = CalculateRemainingUsableMm(currentDiameter, roll.MinDiameter);
        decimal averageWear = CalculateAverageWearPerGrind(loggedWearMm, loggedGrindCount, roll.RollType, out bool isDefaultRate);
        int estimatedRemainingGrinds = CalculateEstimatedRemainingGrinds(remainingUsableMm, averageWear);
        string status = DetermineStatus(currentDiameter, roll.MinDiameter, latest?.IsAbnormal ?? false);

        var dto = new RollLifetimeViewDto();
        dto.CopyRollFieldsFrom(roll);
        dto.CurrentDiameter = currentDiameter;
        dto.TotalGrindCount = totalGrindCount;
        dto.LoggedGrindCount = loggedGrindCount;
        dto.TotalWearMm = totalWearMm;
        dto.RemainingUsableMm = remainingUsableMm;
        dto.RemainingLifetimePercent = CalculateRemainingLifetimePercent(currentDiameter, roll.MaxDiameter, roll.MinDiameter);
        dto.AverageWearPerGrind = Math.Round(averageWear, 4, MidpointRounding.AwayFromZero);
        dto.IsUsingDefaultWearRate = isDefaultRate;
        dto.EstimatedRemainingGrinds = estimatedRemainingGrinds;
        dto.LastGrindDate = latest?.GrindDate;
        dto.LastGrindIsAbnormal = latest?.IsAbnormal ?? false;
        dto.Status = status;
        dto.EstimatedRemainingGrindsText = dto.IsScrap
            ? "0 (Hết đời)"
            : (loggedGrindCount > 0 && estimatedRemainingGrinds > 0 ? $"~{estimatedRemainingGrinds} lần" : "Chưa mài");
        return dto;
    }

    /// <summary>Dựng các dòng hiển thị nhật ký mài (tab MAI_xxx) cho một cây trục.</summary>
    public static List<GrindingLogViewDto> BuildGrindingLogViews(RollEntity roll, IReadOnlyList<GrindingLogEntity> orderedLogs)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(orderedLogs);

        var result = new List<GrindingLogViewDto>(orderedLogs.Count);
        for (int index = 0; index < orderedLogs.Count; index++)
        {
            GrindingLogEntity log = orderedLogs[index];
            int loggedSeq = index + 1;
            int grindSequence = roll.InitialGrindCount + loggedSeq;
            decimal cumulativeWear = Math.Max(0m, roll.MaxDiameter - log.PostGrindDiameter);
            decimal loggedWearSoFar = Math.Max(0m, roll.InitialDiameter - log.PostGrindDiameter);
            decimal remainingUsable = CalculateRemainingUsableMm(log.PostGrindDiameter, roll.MinDiameter);
            decimal averageWear = CalculateAverageWearPerGrind(loggedWearSoFar, loggedSeq, roll.RollType, out _);

            result.Add(new GrindingLogViewDto
            {
                Id = log.Id,
                RollCode = roll.RollCode,
                RollType = roll.RollType,
                GrindSequence = grindSequence,
                GrindDate = log.GrindDate,
                PreGrindDiameter = log.PreGrindDiameter,
                PostGrindDiameter = log.PostGrindDiameter,
                GrindConsumption = log.PreGrindDiameter - log.PostGrindDiameter,
                CumulativeWear = cumulativeWear,
                RemainingLifetimePercent = CalculateRemainingLifetimePercent(log.PostGrindDiameter, roll.MaxDiameter, roll.MinDiameter),
                EstimatedRemainingGrinds = CalculateEstimatedRemainingGrinds(remainingUsable, averageWear),
                IsAbnormal = log.IsAbnormal,
                ConditionText = GrindConditionTexts.From(log.IsAbnormal),
                Remarks = log.Remarks,
                IsLatestOfRoll = index == orderedLogs.Count - 1
            });
        }

        return result;
    }

    /// <summary>Tổng hợp tiêu hao của một cây trục trong khoảng [periodStart, periodEnd].</summary>
    public static RollPeriodWearDto BuildPeriodWear(
        RollLifetimeViewDto lifetime,
        IReadOnlyList<GrindingLogEntity> orderedLogs,
        DateTime periodStart,
        DateTime periodEnd)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(orderedLogs);

        int grindCount = 0;
        int abnormalCount = 0;
        decimal normalWear = 0m;
        decimal abnormalWear = 0m;

        foreach (GrindingLogEntity log in orderedLogs)
        {
            if (log.GrindDate < periodStart || log.GrindDate > periodEnd)
            {
                continue;
            }

            decimal consumption = log.PreGrindDiameter - log.PostGrindDiameter;
            grindCount++;
            if (log.IsAbnormal)
            {
                abnormalCount++;
                abnormalWear += consumption;
            }
            else
            {
                normalWear += consumption;
            }
        }

        return new RollPeriodWearDto
        {
            RollCode = lifetime.RollCode,
            RollType = lifetime.RollType,
            GrindCountInPeriod = grindCount,
            AbnormalGrindCountInPeriod = abnormalCount,
            NormalWearMm = normalWear,
            AbnormalWearMm = abnormalWear,
            TotalWearMmInPeriod = normalWear + abnormalWear,
            CurrentDiameter = lifetime.CurrentDiameter,
            RemainingUsableMm = lifetime.RemainingUsableMm,
            RemainingLifetimePercent = lifetime.RemainingLifetimePercent,
            Status = lifetime.Status
        };
    }

    public static decimal RoundRatio(decimal value)
    {
        return Math.Round(value, RatioDecimals, MidpointRounding.AwayFromZero);
    }
}
