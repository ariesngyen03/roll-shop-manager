using System.Globalization;
using Microsoft.Data.Sqlite;
using RollShopManager.Database;
using RollShopManager.Models;
using RollShopManager.Security;

namespace RollShopManager.Services;

/// <summary>
/// Tầng nghiệp vụ lõi của Roll Shop:
/// - Quản lý danh mục trục (QUAN_LY_TRUC).
/// - Nhật ký mài (MAI_WR / MAI_IMR / MAI_BUR) với cơ chế tự nối chuỗi đường kính.
/// - Vòng đời trục, báo cáo tiêu hao theo kỳ, KPI mm/tấn và dự báo ngày đặt hàng.
/// Mọi thao tác ghi đều chạy trong SqliteTransaction; thao tác sửa / xóa bắt buộc quyền Admin và được ghi AuditLogs.
/// </summary>
public sealed class RollBusinessService
{
    public const int MaxRollCodeLength = 50;
    public const int MaxRemarksLength = 1000;
    public const int MaxNotesLength = 2000;
    public const int MaxPeriodNameLength = 200;

    /// <summary>Giới hạn số ngày dự báo (100 năm) để tránh tràn DateTime khi tốc độ tiêu hao rất nhỏ.</summary>
    public const int MaxForecastDays = 36_500;

    /// <summary>Số ngày quy chuẩn của 1 tháng sản xuất công nghiệp dùng để tính tốc độ hao (Cây/Tháng).</summary>
    public const decimal StandardDaysPerMonth = 30m;

    private readonly RollRepository _rollRepository;
    private readonly GrindingLogRepository _grindingLogRepository;
    private readonly ProcurementConfigRepository _procurementConfigRepository;
    private readonly ProductionPeriodRepository _productionPeriodRepository;
    private readonly MonthlyProductionRepository _monthlyProductionRepository;
    private readonly AuditLogRepository _auditLogRepository;

    public RollBusinessService()
        : this(
            new RollRepository(),
            new GrindingLogRepository(),
            new ProcurementConfigRepository(),
            new ProductionPeriodRepository(),
            new MonthlyProductionRepository(),
            new AuditLogRepository())
    {
    }

    public RollBusinessService(
        RollRepository rollRepository,
        GrindingLogRepository grindingLogRepository,
        ProcurementConfigRepository procurementConfigRepository,
        ProductionPeriodRepository productionPeriodRepository,
        MonthlyProductionRepository monthlyProductionRepository,
        AuditLogRepository auditLogRepository)
    {
        _rollRepository = rollRepository ?? throw new ArgumentNullException(nameof(rollRepository));
        _grindingLogRepository = grindingLogRepository ?? throw new ArgumentNullException(nameof(grindingLogRepository));
        _procurementConfigRepository = procurementConfigRepository ?? throw new ArgumentNullException(nameof(procurementConfigRepository));
        _productionPeriodRepository = productionPeriodRepository ?? throw new ArgumentNullException(nameof(productionPeriodRepository));
        _monthlyProductionRepository = monthlyProductionRepository ?? throw new ArgumentNullException(nameof(monthlyProductionRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
    }

    // =====================================================================================
    // 1. DANH MỤC TRỤC (QUAN_LY_TRUC)
    // =====================================================================================

    public IReadOnlyList<RollEntity> GetAllRolls(string? rollType = null)
    {
        if (rollType is null)
        {
            return _rollRepository.GetAll();
        }

        return _rollRepository.GetByType(ValidateRollType(rollType));
    }

    public RollEntity? GetRoll(string rollCode)
    {
        return _rollRepository.GetByCode(NormalizeRollCode(rollCode));
    }

    /// <summary>Khai báo trục mới (chỉ Admin). InitialDiameter &lt;= 0 sẽ được hiểu là trục mới: InitialDiameter = MaxDiameter.</summary>
    public RollEntity CreateRoll(RollEntity roll)
    {
        AuthorizationGuard.EnsureAdmin("Khai báo trục mới");
        ArgumentNullException.ThrowIfNull(roll);

        RollEntity normalized = NormalizeRoll(roll);
        if (normalized.InitialDiameter <= 0m)
        {
            normalized.InitialDiameter = normalized.MaxDiameter;
        }

        ValidateRollDimensions(normalized);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (_rollRepository.Exists(normalized.RollCode, connection, transaction))
        {
            throw new InvalidOperationException($"Mã trục '{normalized.RollCode}' đã tồn tại.");
        }

        _rollRepository.Insert(normalized, connection, transaction);
        WriteAudit(connection, transaction, AuditActionTypes.CreateRoll, "Rolls",
            $"Khai báo trục mới: {DescribeRoll(normalized)}");

        transaction.Commit();
        return normalized;
    }

    /// <summary>
    /// Sửa thông tin trục (chỉ Admin). Không được đổi mã trục. Không được đổi loại trục khi đã có nhật ký mài.
    /// Nếu thay đổi Max / Min / Initial Diameter thì chuỗi nhật ký mài được nối lại và kiểm tra hợp lệ.
    /// </summary>
    public RollEntity UpdateRoll(RollEntity roll)
    {
        AuthorizationGuard.EnsureAdmin("Sửa thông tin trục");
        ArgumentNullException.ThrowIfNull(roll);

        RollEntity normalized = NormalizeRoll(roll);
        if (normalized.InitialDiameter <= 0m)
        {
            normalized.InitialDiameter = normalized.MaxDiameter;
        }

        ValidateRollDimensions(normalized);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        RollEntity existing = _rollRepository.GetByCode(normalized.RollCode, connection, transaction)
                              ?? throw new KeyNotFoundException($"Không tìm thấy trục '{normalized.RollCode}'.");

        int logCount = _grindingLogRepository.CountByRollCode(normalized.RollCode, connection, transaction);
        if (!string.Equals(existing.RollType, normalized.RollType, StringComparison.Ordinal) && logCount > 0)
        {
            throw new InvalidOperationException(
                $"Không thể đổi loại trục '{normalized.RollCode}' từ {existing.RollType} sang {normalized.RollType} " +
                $"vì trục đã có {logCount} dòng nhật ký mài.");
        }

        normalized.RollCode = existing.RollCode;
        _rollRepository.Update(normalized, connection, transaction);

        bool geometryChanged = existing.MaxDiameter != normalized.MaxDiameter ||
                               existing.MinDiameter != normalized.MinDiameter ||
                               existing.InitialDiameter != normalized.InitialDiameter;

        int rechainedRows = 0;
        if (geometryChanged && logCount > 0)
        {
            rechainedRows = RecalculateRollChainCore(connection, transaction, normalized, validateChain: true);
        }

        WriteAudit(connection, transaction, AuditActionTypes.UpdateRoll, "Rolls",
            $"TRƯỚC: {DescribeRoll(existing)} | SAU: {DescribeRoll(normalized)} | Số dòng nhật ký được nối lại ĐK trước mài: {rechainedRows}");

        transaction.Commit();
        return normalized;
    }

    /// <summary>Xóa trục và toàn bộ nhật ký mài của trục đó (chỉ Admin). Trả về số dòng nhật ký mài đã xóa.</summary>
    public int DeleteRoll(string rollCode, string? reason = null)
    {
        AuthorizationGuard.EnsureAdmin("Xóa trục");
        string code = NormalizeRollCode(rollCode);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        RollEntity existing = _rollRepository.GetByCode(code, connection, transaction)
                              ?? throw new KeyNotFoundException($"Không tìm thấy trục '{code}'.");

        IReadOnlyList<GrindingLogEntity> logs = _grindingLogRepository.GetByRollCode(existing.RollCode, connection, transaction);
        int deletedLogs = _grindingLogRepository.DeleteByRollCode(existing.RollCode, connection, transaction);
        _rollRepository.Delete(existing.RollCode, connection, transaction);

        string logSummary = logs.Count == 0
            ? "Không có nhật ký mài."
            : $"Đã xóa {deletedLogs} dòng nhật ký mài (từ {logs.Min(l => l.GrindDate):yyyy-MM-dd} đến {logs.Max(l => l.GrindDate):yyyy-MM-dd}, " +
              $"ĐK cuối {FormatMm(RollCalculator.OrderChain(logs)[^1].PostGrindDiameter)}).";

        WriteAudit(connection, transaction, AuditActionTypes.DeleteRoll, "Rolls",
            $"Xóa trục: {DescribeRoll(existing)} | {logSummary} | Lý do: {NormalizeText(reason) ?? "(không ghi)"}");

        transaction.Commit();
        return deletedLogs;
    }

    // =====================================================================================
    // 2. NHẬT KÝ MÀI (MAI_WR / MAI_IMR / MAI_BUR)
    // =====================================================================================

    /// <summary>
    /// Đường kính trước mài cho ca mài tiếp theo:
    /// - Chưa có nhật ký mài -> RollEntity.InitialDiameter (mặc định = MaxDiameter).
    /// - Đã có -> PostGrindDiameter của lần mài mới nhất.
    /// </summary>
    public decimal GetPreGrindDiameter(string rollCode)
    {
        string code = NormalizeRollCode(rollCode);
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        return GetPreGrindDiameter(connection, null, code);
    }

    /// <summary>
    /// Thêm mới ca mài (Operator và Admin đều được phép).
    /// - Operator chỉ được nhập theo trình tự thời gian (không sớm hơn ca mài mới nhất của trục).
    /// - Admin được phép chèn bổ sung ca mài vào quá khứ; khi đó chuỗi được nối lại và kiểm tra hợp lệ, có ghi AuditLogs.
    /// - PreGrindDiameter do hệ thống tự xác định, công nhân chỉ nhập PostGrindDiameter.
    /// </summary>
    public GrindingLogEntity AddGrindingLog(string rollCode, DateTime grindDate, decimal postGrindDiameter, bool isAbnormal, string? remarks)
    {
        string code = NormalizeRollCode(rollCode);
        string? normalizedRemarks = ValidateGrindInput(grindDate, postGrindDiameter, isAbnormal, remarks);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        RollEntity roll = _rollRepository.GetByCode(code, connection, transaction)
                          ?? throw new KeyNotFoundException($"Không tìm thấy trục '{code}'. Vui lòng khai báo trục tại QUAN_LY_TRUC trước.");

        if (grindDate.Date < roll.CommissionDate.Date)
        {
            throw new ArgumentException(
                $"Ngày mài ({grindDate:dd/MM/yyyy}) không được trước ngày đưa trục vào sử dụng ({roll.CommissionDate:dd/MM/yyyy}).",
                nameof(grindDate));
        }

        List<GrindingLogEntity> chain = RollCalculator.OrderChain(_grindingLogRepository.GetByRollCode(roll.RollCode, connection, transaction));
        GrindingLogEntity? latest = chain.Count > 0 ? chain[^1] : null;
        bool isBackdated = latest is not null && grindDate < latest.GrindDate;

        decimal preGrindDiameter;
        if (isBackdated)
        {
            if (!AppSession.IsAdmin)
            {
                throw new PermissionDeniedException(
                    $"Ngày mài ({grindDate:dd/MM/yyyy HH:mm}) sớm hơn ca mài gần nhất của trục {roll.RollCode} " +
                    $"({latest!.GrindDate:dd/MM/yyyy HH:mm}). Chèn bổ sung ca mài vào quá khứ yêu cầu quyền Quản lý (Admin).");
            }

            GrindingLogEntity? predecessor = chain.LastOrDefault(log => log.GrindDate <= grindDate);
            preGrindDiameter = predecessor?.PostGrindDiameter ?? roll.InitialDiameter;
        }
        else
        {
            preGrindDiameter = latest?.PostGrindDiameter ?? roll.InitialDiameter;
            if (preGrindDiameter <= roll.MinDiameter)
            {
                throw new InvalidOperationException(
                    $"Trục {roll.RollCode} đã hết đời (ĐK hiện tại {FormatMm(preGrindDiameter)} ≤ ĐK phế phẩm {FormatMm(roll.MinDiameter)}). Không thể ghi thêm ca mài.");
            }
        }

        if (postGrindDiameter >= preGrindDiameter)
        {
            throw new ArgumentException(
                $"ĐK sau mài ({FormatMm(postGrindDiameter)}) phải nhỏ hơn ĐK trước mài ({FormatMm(preGrindDiameter)}) của trục {roll.RollCode}.",
                nameof(postGrindDiameter));
        }

        var entity = new GrindingLogEntity
        {
            RollCode = roll.RollCode,
            GrindDate = grindDate,
            PreGrindDiameter = preGrindDiameter,
            PostGrindDiameter = postGrindDiameter,
            IsAbnormal = isAbnormal,
            Remarks = normalizedRemarks
        };

        entity.Id = _grindingLogRepository.Insert(entity, connection, transaction);

        if (isBackdated)
        {
            int rechainedRows = RecalculateRollChainCore(connection, transaction, roll, validateChain: true);
            WriteAudit(connection, transaction, AuditActionTypes.InsertBackdatedGrind, "GrindingLogs",
                $"Admin chèn bổ sung ca mài vào quá khứ: {DescribeLog(entity)} | Số dòng phía sau được nối lại ĐK trước mài: {rechainedRows}");
        }

        transaction.Commit();
        AppSession.RegisterActivity();
        return entity;
    }

    /// <summary>Sửa một dòng nhật ký mài (chỉ Admin). Chuỗi ĐK của trục được nối lại và kiểm tra hợp lệ trong cùng transaction.</summary>
    public GrindingLogEntity UpdateGrindingLog(int id, DateTime grindDate, decimal postGrindDiameter, bool isAbnormal, string? remarks)
    {
        AuthorizationGuard.EnsureAdmin("Sửa nhật ký mài");
        string? normalizedRemarks = ValidateGrindInput(grindDate, postGrindDiameter, isAbnormal, remarks);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        GrindingLogEntity existing = _grindingLogRepository.GetById(id, connection, transaction)
                                     ?? throw new KeyNotFoundException($"Không tìm thấy dòng nhật ký mài Id = {id}.");
        RollEntity roll = _rollRepository.GetByCode(existing.RollCode, connection, transaction)
                          ?? throw new InvalidOperationException($"Dữ liệu lỗi: không tìm thấy trục '{existing.RollCode}' của nhật ký mài Id = {id}.");

        if (grindDate.Date < roll.CommissionDate.Date)
        {
            throw new ArgumentException(
                $"Ngày mài ({grindDate:dd/MM/yyyy}) không được trước ngày đưa trục vào sử dụng ({roll.CommissionDate:dd/MM/yyyy}).",
                nameof(grindDate));
        }

        GrindingLogEntity updated = existing.Clone();
        updated.GrindDate = grindDate;
        updated.PostGrindDiameter = postGrindDiameter;
        updated.IsAbnormal = isAbnormal;
        updated.Remarks = normalizedRemarks;

        _grindingLogRepository.Update(updated, connection, transaction);
        int rechainedRows = RecalculateRollChainCore(connection, transaction, roll, validateChain: true);

        GrindingLogEntity reloaded = _grindingLogRepository.GetById(id, connection, transaction)
                                     ?? throw new InvalidOperationException($"Không đọc lại được nhật ký mài Id = {id} sau khi cập nhật.");

        WriteAudit(connection, transaction, AuditActionTypes.UpdateGrind, "GrindingLogs",
            $"TRƯỚC: {DescribeLog(existing)} | SAU: {DescribeLog(reloaded)} | Số dòng được nối lại ĐK trước mài: {rechainedRows}");

        transaction.Commit();
        return reloaded;
    }

    /// <summary>Xóa một dòng nhật ký mài (chỉ Admin). Chuỗi ĐK của trục được nối lại trong cùng transaction.</summary>
    public void DeleteGrindingLog(int id, string? reason = null)
    {
        AuthorizationGuard.EnsureAdmin("Xóa nhật ký mài");

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        GrindingLogEntity existing = _grindingLogRepository.GetById(id, connection, transaction)
                                     ?? throw new KeyNotFoundException($"Không tìm thấy dòng nhật ký mài Id = {id}.");
        RollEntity roll = _rollRepository.GetByCode(existing.RollCode, connection, transaction)
                          ?? throw new InvalidOperationException($"Dữ liệu lỗi: không tìm thấy trục '{existing.RollCode}' của nhật ký mài Id = {id}.");

        _grindingLogRepository.Delete(id, connection, transaction);
        int rechainedRows = RecalculateRollChainCore(connection, transaction, roll, validateChain: true);

        WriteAudit(connection, transaction, AuditActionTypes.DeleteGrind, "GrindingLogs",
            $"Xóa: {DescribeLog(existing)} | Số dòng được nối lại ĐK trước mài: {rechainedRows} | Lý do: {NormalizeText(reason) ?? "(không ghi)"}");

        transaction.Commit();
    }

    /// <summary>
    /// Tính toán lại chuỗi lịch sử mài của một trục: sắp xếp GrindingLogs theo GrindDate ASC, Id ASC và nối đuôi
    /// PreGrindDiameter(n) = PostGrindDiameter(n-1), dòng đầu tiên = InitialDiameter. Chạy trong một SqliteTransaction.
    /// </summary>
    /// <returns>Số dòng có PreGrindDiameter bị điều chỉnh.</returns>
    public int RecalculateRollChain(string rollCode)
    {
        string code = NormalizeRollCode(rollCode);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        RollEntity roll = _rollRepository.GetByCode(code, connection, transaction)
                          ?? throw new KeyNotFoundException($"Không tìm thấy trục '{code}'.");

        int rechainedRows = RecalculateRollChainCore(connection, transaction, roll, validateChain: false);
        if (rechainedRows > 0)
        {
            WriteAudit(connection, transaction, AuditActionTypes.RecalculateChain, "GrindingLogs",
                $"Nối lại chuỗi ĐK trước mài cho trục {roll.RollCode}: điều chỉnh {rechainedRows} dòng.");
        }

        transaction.Commit();
        return rechainedRows;
    }

    /// <summary>Nối lại chuỗi cho toàn bộ trục trong xưởng (dùng cho chức năng "Kiểm tra &amp; sửa dữ liệu"). Trả về tổng số dòng được điều chỉnh.</summary>
    public int RecalculateAllRollChains()
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        int total = 0;
        List<string> affectedRolls = new();
        foreach (RollEntity roll in _rollRepository.GetAll(connection, transaction))
        {
            int rechainedRows = RecalculateRollChainCore(connection, transaction, roll, validateChain: false);
            if (rechainedRows > 0)
            {
                total += rechainedRows;
                affectedRolls.Add($"{roll.RollCode}({rechainedRows})");
            }
        }

        if (total > 0)
        {
            WriteAudit(connection, transaction, AuditActionTypes.RecalculateChain, "GrindingLogs",
                $"Nối lại chuỗi ĐK trước mài toàn xưởng: điều chỉnh {total} dòng. Trục bị ảnh hưởng: {string.Join(", ", affectedRolls)}.");
        }

        transaction.Commit();
        return total;
    }

    /// <summary>Danh sách nhật ký mài hiển thị cho tab MAI_WR / MAI_IMR / MAI_BUR.</summary>
    public IReadOnlyList<GrindingLogViewDto> GetGrindingLogViews(string rollType, bool newestFirst = true)
    {
        string type = ValidateRollType(rollType);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        (IReadOnlyList<RollEntity> rolls, Dictionary<string, List<GrindingLogEntity>> chains) =
            LoadRollsWithChains(connection, transaction, type);
        transaction.Commit();

        var views = new List<GrindingLogViewDto>();
        foreach (RollEntity roll in rolls)
        {
            views.AddRange(RollCalculator.BuildGrindingLogViews(roll, GetChain(chains, roll.RollCode)));
        }

        IOrderedEnumerable<GrindingLogViewDto> ordered = newestFirst
            ? views.OrderByDescending(v => v.GrindDate).ThenByDescending(v => v.Id)
            : views.OrderBy(v => v.GrindDate).ThenBy(v => v.Id);
        return ordered.ToList();
    }

    /// <summary>Lịch sử mài của một cây trục (thứ tự thời gian tăng dần).</summary>
    public IReadOnlyList<GrindingLogViewDto> GetGrindingLogViewsByRoll(string rollCode)
    {
        string code = NormalizeRollCode(rollCode);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        RollEntity roll = _rollRepository.GetByCode(code, connection, transaction)
                          ?? throw new KeyNotFoundException($"Không tìm thấy trục '{code}'.");
        List<GrindingLogEntity> chain = RollCalculator.OrderChain(_grindingLogRepository.GetByRollCode(roll.RollCode, connection, transaction));
        transaction.Commit();

        return RollCalculator.BuildGrindingLogViews(roll, chain);
    }

    public GrindingLogEntity? GetGrindingLog(int id)
    {
        return _grindingLogRepository.GetById(id);
    }

    // =====================================================================================
    // 3. VÒNG ĐỜI TRỤC
    // =====================================================================================

    /// <summary>Danh sách vòng đời trục (lọc theo loại nếu có), sắp xếp WR -> IMR -> BUR, rồi theo mã trục.</summary>
    public IReadOnlyList<RollLifetimeViewDto> GetRollLifetimes(string? rollType = null)
    {
        string? type = rollType is null ? null : ValidateRollType(rollType);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        (IReadOnlyList<RollEntity> rolls, Dictionary<string, List<GrindingLogEntity>> chains) =
            LoadRollsWithChains(connection, transaction, type);
        transaction.Commit();

        return rolls
            .Select(roll => RollCalculator.BuildLifetime(roll, GetChain(chains, roll.RollCode)))
            .OrderBy(dto => RollTypes.GetSortOrder(dto.RollType))
            .ThenBy(dto => dto.RollCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public RollLifetimeViewDto GetRollLifetime(string rollCode)
    {
        string code = NormalizeRollCode(rollCode);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        RollEntity roll = _rollRepository.GetByCode(code, connection, transaction)
                          ?? throw new KeyNotFoundException($"Không tìm thấy trục '{code}'.");
        List<GrindingLogEntity> chain = RollCalculator.OrderChain(_grindingLogRepository.GetByRollCode(roll.RollCode, connection, transaction));
        transaction.Commit();

        return RollCalculator.BuildLifetime(roll, chain);
    }

    /// <summary>Ước tính số lần mài còn lại của một trục = Floor(RemainingUsableMm / AverageWearPerGrind).</summary>
    public int GetEstimatedRemainingGrinds(string rollCode)
    {
        return GetRollLifetime(rollCode).EstimatedRemainingGrinds;
    }

    // =====================================================================================
    // 4. BÁO CÁO TIÊU HAO THEO KỲ & DỰ BÁO ĐẶT HÀNG
    // =====================================================================================

    /// <summary>
    /// Bảng chi tiết tiêu hao từng cây trục từ Ngày A đến Ngày B (bao trọn cả ngày B).
    /// Bao gồm các trục còn hoạt động và các trục đã Scrap nhưng có phát sinh mài trong kỳ.
    /// </summary>
    public IReadOnlyList<RollPeriodWearDto> GetPeriodWearDetails(DateTime fromDate, DateTime toDate, string? rollType = null)
    {
        (DateTime periodStart, DateTime periodEnd) = NormalizePeriod(fromDate, toDate);
        string? type = rollType is null ? null : ValidateRollType(rollType);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        (IReadOnlyList<RollEntity> rolls, Dictionary<string, List<GrindingLogEntity>> chains) =
            LoadRollsWithChains(connection, transaction, type);
        transaction.Commit();

        return BuildPeriodWearRows(rolls, chains, periodStart, periodEnd)
            .Select(row => row.PeriodWear)
            .ToList();
    }

    /// <summary>
    /// Bảng tổng hợp KPI &amp; Cảnh báo đặt hàng theo loại trục (WR, IMR, BUR) cho kỳ Ngày A -> Ngày B với sản lượng ProductionTons.
    /// </summary>
    public IReadOnlyList<TypeConsumptionAndForecastDto> GetConsumptionAndForecast(DateTime fromDate, DateTime toDate, decimal productionTons)
    {
        (DateTime periodStart, DateTime periodEnd) = NormalizePeriod(fromDate, toDate);
        if (productionTons <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(productionTons), productionTons,
                "Sản lượng trong kỳ (tấn) phải lớn hơn 0.");
        }

        int periodDays = (periodEnd.Date - periodStart.Date).Days + 1;
        DateTime today = DateTime.Today;

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        (IReadOnlyList<RollEntity> rolls, Dictionary<string, List<GrindingLogEntity>> chains) =
            LoadRollsWithChains(connection, transaction, null);
        Dictionary<string, ProcurementConfigEntity> configs = _procurementConfigRepository
            .GetAll(connection, transaction)
            .ToDictionary(c => c.RollType, StringComparer.OrdinalIgnoreCase);
        transaction.Commit();

        List<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)> rows =
            BuildPeriodWearRows(rolls, chains, periodStart, periodEnd);

        // Xác định số ngày vận hành thực tế trong kỳ (không tính các ngày tương lai > Today hoặc các tháng trống đầu năm)
        DateTime cappedEnd = periodEnd.Date > today ? today : periodEnd.Date;
        DateTime effectiveStart = periodStart.Date;
        if ((cappedEnd - effectiveStart).Days + 1 > 35)
        {
            DateTime? earliestLogInPeriod = chains.Values
                .SelectMany(c => c)
                .Where(l => l.GrindDate >= periodStart && l.GrindDate <= periodEnd)
                .Select(l => (DateTime?)l.GrindDate.Date)
                .Min();

            if (earliestLogInPeriod.HasValue && earliestLogInPeriod.Value > effectiveStart)
            {
                DateTime firstDayActiveMonth = new(earliestLogInPeriod.Value.Year, earliestLogInPeriod.Value.Month, 1);
                if (firstDayActiveMonth > effectiveStart)
                {
                    effectiveStart = firstDayActiveMonth;
                }
            }
        }

        int effectiveActiveDays = cappedEnd >= effectiveStart
            ? Math.Max(1, (cappedEnd - effectiveStart).Days + 1)
            : periodDays;

        // Tính nhịp độ tiêu hao cơ sở 30 ngày gần nhất của xưởng để dự báo khi xem kỳ kế hoạch (chưa có ca mài)
        DateTime baselineStart = today.AddDays(-29);
        DateTime baselineEnd = today.AddDays(1).AddTicks(-1);
        List<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)> baselineRows =
            BuildPeriodWearRows(rolls, chains, baselineStart, baselineEnd);

        var result = new List<TypeConsumptionAndForecastDto>(RollTypes.All.Count);
        foreach (string type in RollTypes.All)
        {
            if (!configs.TryGetValue(type, out ProcurementConfigEntity? config))
            {
                throw new InvalidOperationException(
                    $"Chưa có cấu hình đặt hàng cho loại trục {type}. Vui lòng kiểm tra bảng ProcurementConfigs.");
            }

            if (config.StandardUsableMmPerRoll <= 0m)
            {
                throw new InvalidOperationException(
                    $"Cấu hình đặt hàng của {type} không hợp lệ: StandardUsableMmPerRoll phải lớn hơn 0.");
            }

            List<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)> typeRows =
                rows.Where(r => r.Lifetime.RollType == type).ToList();

            decimal baseline30DayWearMm = baselineRows
                .Where(r => r.Lifetime.RollType == type)
                .Sum(r => r.PeriodWear.TotalWearMmInPeriod);

            result.Add(BuildTypeForecast(
                type, config, typeRows, periodStart, periodEnd,
                periodDays, effectiveActiveDays, productionTons, baseline30DayWearMm, today));
        }

        return result;
    }

    /// <summary>Báo cáo KPI &amp; dự báo cho một kỳ báo cáo đã lưu.</summary>
    public IReadOnlyList<TypeConsumptionAndForecastDto> GetConsumptionAndForecast(ProductionPeriodEntity period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return GetConsumptionAndForecast(period.FromDate, period.ToDate, period.ProductionTons);
    }

    // =====================================================================================
    // 5. KỲ BÁO CÁO SẢN LƯỢNG
    // =====================================================================================

    public IReadOnlyList<ProductionPeriodEntity> GetProductionPeriods()
    {
        return _productionPeriodRepository.GetAll();
    }

    public ProductionPeriodEntity? GetProductionPeriod(int id)
    {
        return _productionPeriodRepository.GetById(id);
    }

    /// <summary>Lưu kỳ báo cáo (Id = 0: thêm mới, Id &gt; 0: cập nhật). Chỉ Admin.</summary>
    public ProductionPeriodEntity SaveProductionPeriod(ProductionPeriodEntity period)
    {
        AuthorizationGuard.EnsureAdmin("Lưu kỳ báo cáo sản lượng");
        ArgumentNullException.ThrowIfNull(period);

        ProductionPeriodEntity normalized = period.Clone();
        normalized.PeriodName = (normalized.PeriodName ?? string.Empty).Trim();
        normalized.FromDate = normalized.FromDate.Date;
        normalized.ToDate = normalized.ToDate.Date;

        if (normalized.PeriodName.Length == 0)
        {
            throw new ArgumentException("Tên kỳ báo cáo không được để trống.", nameof(period));
        }

        if (normalized.PeriodName.Length > MaxPeriodNameLength)
        {
            throw new ArgumentException($"Tên kỳ báo cáo không được vượt quá {MaxPeriodNameLength} ký tự.", nameof(period));
        }

        if (normalized.FromDate > normalized.ToDate)
        {
            throw new ArgumentException("Ngày bắt đầu kỳ phải nhỏ hơn hoặc bằng ngày kết thúc kỳ.", nameof(period));
        }

        if (normalized.ProductionTons <= 0m)
        {
            throw new ArgumentException("Sản lượng trong kỳ (tấn) phải lớn hơn 0.", nameof(period));
        }

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (normalized.Id <= 0)
        {
            normalized.Id = _productionPeriodRepository.Insert(normalized, connection, transaction);
            WriteAudit(connection, transaction, AuditActionTypes.CreateProductionPeriod, "ProductionPeriods",
                $"Thêm kỳ báo cáo: {DescribePeriod(normalized)}");
        }
        else
        {
            ProductionPeriodEntity existing = _productionPeriodRepository.GetById(normalized.Id, connection, transaction)
                                              ?? throw new KeyNotFoundException($"Không tìm thấy kỳ báo cáo Id = {normalized.Id}.");
            _productionPeriodRepository.Update(normalized, connection, transaction);
            WriteAudit(connection, transaction, AuditActionTypes.UpdateProductionPeriod, "ProductionPeriods",
                $"TRƯỚC: {DescribePeriod(existing)} | SAU: {DescribePeriod(normalized)}");
        }

        transaction.Commit();
        return normalized;
    }

    public void DeleteProductionPeriod(int id)
    {
        AuthorizationGuard.EnsureAdmin("Xóa kỳ báo cáo sản lượng");

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        ProductionPeriodEntity existing = _productionPeriodRepository.GetById(id, connection, transaction)
                                          ?? throw new KeyNotFoundException($"Không tìm thấy kỳ báo cáo Id = {id}.");
        _productionPeriodRepository.Delete(id, connection, transaction);
        WriteAudit(connection, transaction, AuditActionTypes.DeleteProductionPeriod, "ProductionPeriods",
            $"Xóa kỳ báo cáo: {DescribePeriod(existing)}");

        transaction.Commit();
    }

    // =====================================================================================
    // 5B. MA TRẬN TIÊU HAO 12 THÁNG & SẢN LƯỢNG THÁNG (MONTHLY MATRIX & TONNAGE)
    // =====================================================================================

    /// <summary>
    /// Lấy ma trận tiêu hao 12 tháng của một năm kèm dòng tổng cộng / trung bình cả năm.
    /// Tự động gom nhóm toàn bộ ca mài trong năm theo tháng và 3 loại trục (WR, IMR, BUR),
    /// kết hợp sản lượng tháng để tính định mức mm/1.000 tấn và số cây quy đổi.
    /// </summary>
    public IReadOnlyList<MonthlyConsumptionMatrixDto> GetYearlyMonthlyMatrix(int year)
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);

        IReadOnlyList<RollEntity> rolls = _rollRepository.GetAll(connection, transaction);
        Dictionary<string, string> rollTypeMap = rolls.ToDictionary(r => r.RollCode, r => r.RollType, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ProcurementConfigEntity> configs = _procurementConfigRepository
            .GetAll(connection, transaction)
            .ToDictionary(c => c.RollType, StringComparer.OrdinalIgnoreCase);

        decimal wrStdMm = configs.TryGetValue(RollTypes.WR, out var wrC) && wrC.StandardUsableMmPerRoll > 0m
            ? wrC.StandardUsableMmPerRoll
            : 50m;
        decimal imrStdMm = configs.TryGetValue(RollTypes.IMR, out var imrC) && imrC.StandardUsableMmPerRoll > 0m
            ? imrC.StandardUsableMmPerRoll
            : 50m;
        decimal burStdMm = configs.TryGetValue(RollTypes.BUR, out var burC) && burC.StandardUsableMmPerRoll > 0m
            ? burC.StandardUsableMmPerRoll
            : 80m;

        IReadOnlyList<MonthlyProductionEntity> monthlyProductions = _monthlyProductionRepository.GetByYear(year, connection, transaction);
        Dictionary<int, MonthlyProductionEntity> prodByMonth = monthlyProductions.ToDictionary(p => p.Month);

        DateTime startOfYear = new(year, 1, 1, 0, 0, 0);
        DateTime endOfYear = new(year, 12, 31, 23, 59, 59, 999);
        IReadOnlyList<GrindingLogEntity> yearLogs = _grindingLogRepository.GetByDateRange(startOfYear, endOfYear, connection, transaction);

        transaction.Commit();

        var result = new List<MonthlyConsumptionMatrixDto>(13);
        MonthlyConsumptionMatrixDto? previousMonthDto = null;

        for (int m = 1; m <= 12; m++)
        {
            string ym = $"{year:D4}-{m:D2}";
            string monthDisplay = $"Tháng {m:D2}/{year}";
            decimal tons = prodByMonth.TryGetValue(m, out var prod) ? prod.ProductionTons : 0m;

            var currentMonthLogs = yearLogs.Where(l => l.GrindDate.Month == m).ToList();

            var dto = new MonthlyConsumptionMatrixDto
            {
                YearMonth = ym,
                MonthDisplay = monthDisplay,
                MonthIndex = m,
                ProductionTons = tons,
                IsSummaryRow = false
            };

            // 1. WR
            var wrLogs = currentMonthLogs.Where(l => rollTypeMap.TryGetValue(l.RollCode, out string? t) && t == RollTypes.WR).ToList();
            dto.WrGrindCount = wrLogs.Count;
            dto.WrNormalWearMm = wrLogs.Where(l => !l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.WrAbnormalWearMm = wrLogs.Where(l => l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.WrTotalWearMm = dto.WrNormalWearMm + dto.WrAbnormalWearMm;
            dto.WrWearPerTon = tons > 0m ? RollCalculator.RoundRatio(dto.WrTotalWearMm / tons) : 0m;
            dto.WrWearPer1000Tons = tons > 0m ? (dto.WrTotalWearMm / tons) * 1000m : 0m;
            dto.WrEquivalentRollsUsed = wrStdMm > 0m ? dto.WrTotalWearMm / wrStdMm : 0m;

            // 2. IMR
            var imrLogs = currentMonthLogs.Where(l => rollTypeMap.TryGetValue(l.RollCode, out string? t) && t == RollTypes.IMR).ToList();
            dto.ImrGrindCount = imrLogs.Count;
            dto.ImrNormalWearMm = imrLogs.Where(l => !l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.ImrAbnormalWearMm = imrLogs.Where(l => l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.ImrTotalWearMm = dto.ImrNormalWearMm + dto.ImrAbnormalWearMm;
            dto.ImrWearPerTon = tons > 0m ? RollCalculator.RoundRatio(dto.ImrTotalWearMm / tons) : 0m;
            dto.ImrWearPer1000Tons = tons > 0m ? (dto.ImrTotalWearMm / tons) * 1000m : 0m;
            dto.ImrEquivalentRollsUsed = imrStdMm > 0m ? dto.ImrTotalWearMm / imrStdMm : 0m;

            // 3. BUR
            var burLogs = currentMonthLogs.Where(l => rollTypeMap.TryGetValue(l.RollCode, out string? t) && t == RollTypes.BUR).ToList();
            dto.BurGrindCount = burLogs.Count;
            dto.BurNormalWearMm = burLogs.Where(l => !l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.BurAbnormalWearMm = burLogs.Where(l => l.IsAbnormal).Sum(l => l.PreGrindDiameter - l.PostGrindDiameter);
            dto.BurTotalWearMm = dto.BurNormalWearMm + dto.BurAbnormalWearMm;
            dto.BurWearPerTon = tons > 0m ? RollCalculator.RoundRatio(dto.BurTotalWearMm / tons) : 0m;
            dto.BurWearPer1000Tons = tons > 0m ? (dto.BurTotalWearMm / tons) * 1000m : 0m;
            dto.BurEquivalentRollsUsed = burStdMm > 0m ? dto.BurTotalWearMm / burStdMm : 0m;

            dto.TotalAllRollsWearMm = dto.WrTotalWearMm + dto.ImrTotalWearMm + dto.BurTotalWearMm;

            // MoM Trend %: Chỉ tính khi cả tháng này và tháng liền trước đều có phát sinh mài thực tế
            if (previousMonthDto is not null && dto.TotalAllRollsWearMm > 0m && previousMonthDto.TotalAllRollsWearMm > 0m)
            {
                if (dto.ProductionTons > 0m && previousMonthDto.ProductionTons > 0m && previousMonthDto.TotalWearPer1000Tons > 0m)
                {
                    decimal currRate = dto.TotalWearPer1000Tons;
                    decimal prevRate = previousMonthDto.TotalWearPer1000Tons;
                    dto.MomTrendPercent = Math.Round(((currRate - prevRate) / prevRate) * 100m, 1);
                }
                else
                {
                    decimal currMm = dto.TotalAllRollsWearMm;
                    decimal prevMm = previousMonthDto.TotalAllRollsWearMm;
                    dto.MomTrendPercent = Math.Round(((currMm - prevMm) / prevMm) * 100m, 1);
                }
            }

            result.Add(dto);
            previousMonthDto = dto;
        }

        // Dòng tổng cộng cả năm (Summary Row):
        // Định mức mm/1.000T cả năm tính trên sản lượng của các tháng thực sự có phát sinh mài (hoặc tổng năm nếu đồng nhất)
        decimal totalYearTons = result.Sum(m => m.ProductionTons);
        decimal activeMonthsTons = result.Where(m => m.TotalAllRollsWearMm > 0m).Sum(m => m.ProductionTons);
        decimal effectiveYearTons = activeMonthsTons > 0m ? activeMonthsTons : totalYearTons;

        decimal wrTotalWear = result.Sum(m => m.WrTotalWearMm);
        decimal imrTotalWear = result.Sum(m => m.ImrTotalWearMm);
        decimal burTotalWear = result.Sum(m => m.BurTotalWearMm);

        var summaryDto = new MonthlyConsumptionMatrixDto
        {
            YearMonth = $"{year}-ALL",
            MonthDisplay = $"TỔNG CỘNG NĂM {year}",
            MonthIndex = 0,
            ProductionTons = totalYearTons,
            IsSummaryRow = true,

            WrGrindCount = result.Sum(m => m.WrGrindCount),
            WrNormalWearMm = result.Sum(m => m.WrNormalWearMm),
            WrAbnormalWearMm = result.Sum(m => m.WrAbnormalWearMm),
            WrTotalWearMm = wrTotalWear,
            WrWearPerTon = effectiveYearTons > 0m ? RollCalculator.RoundRatio(wrTotalWear / effectiveYearTons) : 0m,
            WrWearPer1000Tons = effectiveYearTons > 0m ? (wrTotalWear / effectiveYearTons) * 1000m : 0m,
            WrEquivalentRollsUsed = result.Sum(m => m.WrEquivalentRollsUsed),

            ImrGrindCount = result.Sum(m => m.ImrGrindCount),
            ImrNormalWearMm = result.Sum(m => m.ImrNormalWearMm),
            ImrAbnormalWearMm = result.Sum(m => m.ImrAbnormalWearMm),
            ImrTotalWearMm = imrTotalWear,
            ImrWearPerTon = effectiveYearTons > 0m ? RollCalculator.RoundRatio(imrTotalWear / effectiveYearTons) : 0m,
            ImrWearPer1000Tons = effectiveYearTons > 0m ? (imrTotalWear / effectiveYearTons) * 1000m : 0m,
            ImrEquivalentRollsUsed = result.Sum(m => m.ImrEquivalentRollsUsed),

            BurGrindCount = result.Sum(m => m.BurGrindCount),
            BurNormalWearMm = result.Sum(m => m.BurNormalWearMm),
            BurAbnormalWearMm = result.Sum(m => m.BurAbnormalWearMm),
            BurTotalWearMm = burTotalWear,
            BurWearPerTon = effectiveYearTons > 0m ? RollCalculator.RoundRatio(burTotalWear / effectiveYearTons) : 0m,
            BurWearPer1000Tons = effectiveYearTons > 0m ? (burTotalWear / effectiveYearTons) * 1000m : 0m,
            BurEquivalentRollsUsed = result.Sum(m => m.BurEquivalentRollsUsed),

            TotalAllRollsWearMm = wrTotalWear + imrTotalWear + burTotalWear,
            MomTrendPercent = null
        };

        result.Add(summaryDto);
        return result;
    }

    public MonthlyProductionEntity? GetMonthlyProduction(string yearMonth)
    {
        return _monthlyProductionRepository.GetByYearMonth(yearMonth);
    }

    public IReadOnlyList<MonthlyProductionEntity> GetMonthlyProductions(int year)
    {
        return _monthlyProductionRepository.GetByYear(year);
    }

    /// <summary>
    /// Lưu / Cập nhật sản lượng cán thép của một tháng (YYYY-MM). Yêu cầu quyền Admin.
    /// Tự động cập nhật tức thì định mức mm/1.000 tấn cho phân xưởng.
    /// </summary>
    public MonthlyProductionEntity SaveMonthlyProduction(string yearMonth, decimal productionTons, string? notes = null)
    {
        AuthorizationGuard.EnsureAdmin("Cập nhật sản lượng tháng");
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        string ym = yearMonth.Trim();
        string[] parts = ym.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int year) || !int.TryParse(parts[1], out int month) || month < 1 || month > 12)
        {
            throw new ArgumentException($"Định dạng tháng '{yearMonth}' không hợp lệ. Định dạng chuẩn là YYYY-MM (VD: 2026-11).", nameof(yearMonth));
        }

        if (productionTons < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(productionTons), productionTons, "Sản lượng cán thép không được âm.");
        }

        var entity = new MonthlyProductionEntity
        {
            YearMonth = ym,
            Year = year,
            Month = month,
            ProductionTons = productionTons,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        var existing = _monthlyProductionRepository.GetByYearMonth(ym, connection, transaction);
        _monthlyProductionRepository.Upsert(entity, connection, transaction);

        string oldDesc = existing is null ? "Chưa có" : $"{existing.ProductionTons:N0} Tấn";
        WriteAudit(connection, transaction, "UPDATE_MONTHLY_PRODUCTION", "MonthlyProductions",
            $"Cập nhật sản lượng tháng {ym}: [Cũ: {oldDesc}] -> [Mới: {productionTons:N0} Tấn]");

        transaction.Commit();
        return entity;
    }

    public void DeleteMonthlyProduction(string yearMonth)
    {
        AuthorizationGuard.EnsureAdmin("Xóa sản lượng tháng");
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        var existing = _monthlyProductionRepository.GetByYearMonth(yearMonth, connection, transaction)
                       ?? throw new KeyNotFoundException($"Không tìm thấy sản lượng tháng '{yearMonth}'.");

        _monthlyProductionRepository.Delete(yearMonth, connection, transaction);
        WriteAudit(connection, transaction, "DELETE_MONTHLY_PRODUCTION", "MonthlyProductions",
            $"Xóa sản lượng tháng {yearMonth}: {existing.ProductionTons:N0} Tấn");

        transaction.Commit();
    }

    // =====================================================================================
    // 6. CẤU HÌNH ĐẶT HÀNG
    // =====================================================================================

    public IReadOnlyList<ProcurementConfigEntity> GetProcurementConfigs()
    {
        return _procurementConfigRepository.GetAll();
    }

    public ProcurementConfigEntity GetProcurementConfig(string rollType)
    {
        string type = ValidateRollType(rollType);
        return _procurementConfigRepository.GetByType(type)
               ?? throw new KeyNotFoundException($"Chưa có cấu hình đặt hàng cho loại trục {type}.");
    }

    public void UpdateProcurementConfig(ProcurementConfigEntity config)
    {
        AuthorizationGuard.EnsureAdmin("Sửa cấu hình đặt hàng");
        ArgumentNullException.ThrowIfNull(config);

        ProcurementConfigEntity normalized = config.Clone();
        normalized.RollType = ValidateRollType(normalized.RollType);

        if (normalized.StandardUsableMmPerRoll <= 0m)
        {
            throw new ArgumentException("Độ dày lớp vỏ chuẩn (mm/cây) phải lớn hơn 0.", nameof(config));
        }

        if (normalized.LeadTimeDays < 0 || normalized.LeadTimeDays > 3650)
        {
            throw new ArgumentException("Lead time phải nằm trong khoảng 0 - 3650 ngày.", nameof(config));
        }

        if (normalized.SafetyStockRolls < 0m)
        {
            throw new ArgumentException("Tồn kho an toàn (cây) không được âm.", nameof(config));
        }

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        ProcurementConfigEntity? existing = _procurementConfigRepository.GetByType(normalized.RollType, connection, transaction);
        _procurementConfigRepository.Upsert(normalized, connection, transaction);
        WriteAudit(connection, transaction, AuditActionTypes.UpdateProcurementConfig, "ProcurementConfigs",
            $"TRƯỚC: {(existing is null ? "(chưa có)" : DescribeConfig(existing))} | SAU: {DescribeConfig(normalized)}");

        transaction.Commit();
    }

    // =====================================================================================
    // 7. NHẬT KÝ KIỂM TOÁN
    // =====================================================================================

    public IReadOnlyList<AuditLogEntity> GetRecentAuditLogs(int maxRows = 500)
    {
        return _auditLogRepository.GetRecent(maxRows);
    }

    public IReadOnlyList<AuditLogEntity> GetAuditLogs(DateTime fromDate, DateTime toDate)
    {
        (DateTime start, DateTime end) = NormalizePeriod(fromDate, toDate);
        return _auditLogRepository.GetByTimeRange(start, end);
    }

    // =====================================================================================
    // HÀM NỘI BỘ
    // =====================================================================================

    private decimal GetPreGrindDiameter(SqliteConnection connection, SqliteTransaction? transaction, string rollCode)
    {
        RollEntity roll = _rollRepository.GetByCode(rollCode, connection, transaction)
                          ?? throw new KeyNotFoundException($"Không tìm thấy trục '{rollCode}'.");
        GrindingLogEntity? latest = _grindingLogRepository.GetLatestByRollCode(roll.RollCode, connection, transaction);
        return latest?.PostGrindDiameter ?? roll.InitialDiameter;
    }

    /// <summary>
    /// Lõi thuật toán nối chuỗi: chạy trên transaction được truyền vào để gộp chung với thao tác sửa / xóa / chèn.
    /// </summary>
    private int RecalculateRollChainCore(SqliteConnection connection, SqliteTransaction transaction, RollEntity roll, bool validateChain)
    {
        List<GrindingLogEntity> chain = RollCalculator.OrderChain(
            _grindingLogRepository.GetByRollCode(roll.RollCode, connection, transaction));

        decimal expectedPre = roll.InitialDiameter;
        int updatedRows = 0;

        foreach (GrindingLogEntity log in chain)
        {
            if (log.PreGrindDiameter != expectedPre)
            {
                _grindingLogRepository.UpdatePreGrindDiameter(log.Id, expectedPre, connection, transaction);
                log.PreGrindDiameter = expectedPre;
                updatedRows++;
            }

            expectedPre = log.PostGrindDiameter;
        }

        if (validateChain)
        {
            ValidateChain(roll, chain);
        }

        return updatedRows;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ vật lý của chuỗi sau khi nối lại:
    /// - Mỗi ca: PostGrindDiameter &lt; PreGrindDiameter.
    /// - Không có ca mài nào phía sau một ca đã đưa trục về ≤ MinDiameter (trục đã Scrap thì không thể mài tiếp).
    /// Vi phạm -> ném InvalidOperationException để transaction bị rollback.
    /// </summary>
    private static void ValidateChain(RollEntity roll, IReadOnlyList<GrindingLogEntity> orderedChain)
    {
        for (int index = 0; index < orderedChain.Count; index++)
        {
            GrindingLogEntity log = orderedChain[index];

            if (log.PostGrindDiameter >= log.PreGrindDiameter)
            {
                throw new InvalidOperationException(
                    $"Chuỗi nhật ký mài của trục {roll.RollCode} không hợp lệ tại ca mài thứ {roll.InitialGrindCount + index + 1} " +
                    $"(Id = {log.Id}, ngày {log.GrindDate:dd/MM/yyyy HH:mm}): ĐK sau mài {FormatMm(log.PostGrindDiameter)} " +
                    $"không nhỏ hơn ĐK trước mài {FormatMm(log.PreGrindDiameter)}. Thao tác đã bị hủy.");
            }

            if (log.PreGrindDiameter <= roll.MinDiameter)
            {
                throw new InvalidOperationException(
                    $"Chuỗi nhật ký mài của trục {roll.RollCode} không hợp lệ tại ca mài Id = {log.Id} (ngày {log.GrindDate:dd/MM/yyyy HH:mm}): " +
                    $"ĐK trước mài {FormatMm(log.PreGrindDiameter)} đã ≤ ĐK phế phẩm {FormatMm(roll.MinDiameter)} " +
                    "(trục đã hết đời trước ca mài này). Thao tác đã bị hủy.");
            }
        }
    }

    private (IReadOnlyList<RollEntity> Rolls, Dictionary<string, List<GrindingLogEntity>> Chains) LoadRollsWithChains(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? rollType)
    {
        IReadOnlyList<RollEntity> rolls = rollType is null
            ? _rollRepository.GetAll(connection, transaction)
            : _rollRepository.GetByType(rollType, connection, transaction);

        IReadOnlyList<GrindingLogEntity> logs = rollType is null
            ? _grindingLogRepository.GetAll(connection, transaction)
            : _grindingLogRepository.GetByRollType(rollType, connection, transaction);

        Dictionary<string, List<GrindingLogEntity>> chains = logs
            .GroupBy(log => log.RollCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => RollCalculator.OrderChain(group), StringComparer.OrdinalIgnoreCase);

        return (rolls, chains);
    }

    private static IReadOnlyList<GrindingLogEntity> GetChain(Dictionary<string, List<GrindingLogEntity>> chains, string rollCode)
    {
        return chains.TryGetValue(rollCode, out List<GrindingLogEntity>? chain)
            ? chain
            : Array.Empty<GrindingLogEntity>();
    }

    private static List<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)> BuildPeriodWearRows(
        IReadOnlyList<RollEntity> rolls,
        Dictionary<string, List<GrindingLogEntity>> chains,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var rows = new List<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)>(rolls.Count);
        foreach (RollEntity roll in rolls)
        {
            IReadOnlyList<GrindingLogEntity> chain = GetChain(chains, roll.RollCode);
            RollLifetimeViewDto lifetime = RollCalculator.BuildLifetime(roll, chain);
            RollPeriodWearDto periodWear = RollCalculator.BuildPeriodWear(lifetime, chain, periodStart, periodEnd);

            if (lifetime.IsScrap && periodWear.GrindCountInPeriod == 0)
            {
                continue;
            }

            rows.Add((lifetime, periodWear));
        }

        return rows
            .OrderBy(r => RollTypes.GetSortOrder(r.Lifetime.RollType))
            .ThenBy(r => r.Lifetime.RollCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Thuật toán dự báo đặt hàng cho một loại trục:
    ///   TotalWear        = Normal + Abnormal (trong kỳ)
    ///   WearMmPerTon     = TotalWear / ProductionTons;  WearMmPer1000Tons = WearMmPerTon * 1000
    ///   EqRollsUsed      = TotalWear / StandardUsableMmPerRoll
    ///   RemainingMm      = Σ(CurrentDia - MinDia) các trục chưa Scrap;  EqRollsRemaining = RemainingMm / Standard
    ///   DailyBurnMm      = TotalWear / PeriodDays
    ///   MonthlyBurnMm    = DailyBurnMm * 30;  MonthlyBurnRolls = (EqRollsUsed / PeriodDays) * 30
    ///   NetUsableMm      = Max(0, RemainingMm - SafetyStockRolls * Standard)
    ///   RunwayDays       = Floor((NetUsableRolls / MonthlyBurnRolls) * 30) = Floor(NetUsableMm / DailyBurnMm)
    ///   DaysUntilOrder   = RunwayDays - LeadTimeDays;  RecommendedOrderDate = Today + DaysUntilOrder
    /// </summary>
    private static TypeConsumptionAndForecastDto BuildTypeForecast(
        string rollType,
        ProcurementConfigEntity config,
        IReadOnlyList<(RollLifetimeViewDto Lifetime, RollPeriodWearDto PeriodWear)> typeRows,
        DateTime periodStart,
        DateTime periodEnd,
        int periodDays,
        int effectiveActiveDays,
        decimal productionTons,
        decimal baseline30DayWearMm,
        DateTime today)
    {
        decimal standardMm = config.StandardUsableMmPerRoll;
        int safeEffectiveDays = Math.Max(1, effectiveActiveDays);

        int grindCount = typeRows.Sum(r => r.PeriodWear.GrindCountInPeriod);
        int abnormalGrindCount = typeRows.Sum(r => r.PeriodWear.AbnormalGrindCountInPeriod);
        decimal normalWear = typeRows.Sum(r => r.PeriodWear.NormalWearMm);
        decimal abnormalWear = typeRows.Sum(r => r.PeriodWear.AbnormalWearMm);
        decimal totalWear = normalWear + abnormalWear;

        List<RollLifetimeViewDto> activeRolls = typeRows
            .Select(r => r.Lifetime)
            .Where(l => !l.IsScrap)
            .ToList();
        int lowLifeCount = activeRolls.Count(l => l.RemainingLifetimePercent < 15m);
        decimal totalRemainingMm = activeRolls.Sum(l => Math.Max(0m, l.CurrentDiameter - l.MinDiameter));

        decimal wearPerTon = totalWear / productionTons;

        // Tính tốc độ hao dự báo (DailyBurnMm & MonthlyBurnMm):
        // - Nếu kỳ đang chọn có phát sinh mài: tính theo số ngày vận hành thực tế (safeEffectiveDays).
        // - Nếu kỳ đang chọn là kỳ kế hoạch / tháng trống (totalWear == 0) nhưng xưởng đã có lịch sử 30 ngày gần nhất (baseline30DayWearMm > 0):
        //   tự động nội suy tốc độ hao dự kiến theo sản lượng kế hoạch (productionTons) so với sản lượng chuẩn tháng (16,200 Tấn)
        //   để bảng Cảnh báo Đặt hàng luôn hiển thị Runway sống động và phản ứng ngay khi điều chỉnh Sản lượng!
        bool usingBaselineForecast = false;
        decimal dailyBurnMm;
        if (totalWear > 0m)
        {
            dailyBurnMm = totalWear / safeEffectiveDays;
        }
        else if (baseline30DayWearMm > 0m)
        {
            usingBaselineForecast = true;
            decimal tonnageScale = productionTons >= 500m ? (productionTons / 16200m) : 1.0m;
            decimal projectedMonthlyWearMm = baseline30DayWearMm * tonnageScale;
            dailyBurnMm = projectedMonthlyWearMm / StandardDaysPerMonth;
        }
        else
        {
            dailyBurnMm = 0m;
        }

        decimal monthlyBurnMm = dailyBurnMm * StandardDaysPerMonth;
        decimal monthlyBurnRolls = monthlyBurnMm / standardMm;
        decimal safetyStockMm = config.SafetyStockRolls * standardMm;
        decimal netUsableMm = Math.Max(0m, totalRemainingMm - safetyStockMm);

        var dto = new TypeConsumptionAndForecastDto
        {
            RollType = rollType,
            PeriodFromDate = periodStart.Date,
            PeriodToDate = periodEnd.Date,
            PeriodDays = periodDays,
            EffectiveActiveDays = safeEffectiveDays,
            ProductionTons = productionTons,
            GrindCountInPeriod = grindCount,
            AbnormalGrindCountInPeriod = abnormalGrindCount,
            NormalWearMm = normalWear,
            AbnormalWearMm = abnormalWear,
            TotalWearMmInPeriod = totalWear,
            WearMmPerTon = RollCalculator.RoundRatio(wearPerTon),
            WearMmPer1000Tons = RollCalculator.RoundRatio(wearPerTon * 1000m),
            EquivalentRollsConsumed = RollCalculator.RoundRatio(totalWear / standardMm),
            ActiveRollCount = activeRolls.Count,
            LowLifeRollCount = lowLifeCount,
            TotalRemainingMmInShop = totalRemainingMm,
            EquivalentRollsRemaining = RollCalculator.RoundRatio(totalRemainingMm / standardMm),
            StandardUsableMmPerRoll = standardMm,
            LeadTimeDays = config.LeadTimeDays,
            SafetyStockRolls = config.SafetyStockRolls,
            SafetyStockMm = safetyStockMm,
            NetUsableMm = netUsableMm,
            DailyBurnRateMm = RollCalculator.RoundRatio(dailyBurnMm),
            DailyBurnRateRolls = RollCalculator.RoundRatio(dailyBurnMm / standardMm),
            MonthlyBurnRateMm = RollCalculator.RoundRatio(monthlyBurnMm),
            MonthlyBurnRateRolls = RollCalculator.RoundRatio(monthlyBurnRolls)
        };

        if (dailyBurnMm > 0m)
        {
            int runwayDays = ToCappedDays(Math.Floor(netUsableMm / dailyBurnMm));
            int stockoutDays = ToCappedDays(Math.Floor(totalRemainingMm / dailyBurnMm));
            int daysUntilOrderDeadline = runwayDays - config.LeadTimeDays;

            dto.RunwayDays = runwayDays;
            dto.DaysUntilOrderDeadline = daysUntilOrderDeadline;
            dto.RecommendedOrderDate = today.AddDays(daysUntilOrderDeadline);
            dto.SafetyStockReachDate = today.AddDays(runwayDays);
            dto.EstimatedStockoutDate = today.AddDays(stockoutDays);
            dto.AlertStatus = ProcurementAlertStatuses.Classify(daysUntilOrderDeadline);
            string baseMsg = BuildAlertMessage(dto);
            dto.AlertMessage = usingBaselineForecast
                ? $"[Dự báo theo định mức 30 ngày gần nhất] {baseMsg}"
                : baseMsg;
        }
        else if (netUsableMm <= 0m)
        {
            int daysUntilOrderDeadline = -config.LeadTimeDays;

            dto.RunwayDays = 0;
            dto.DaysUntilOrderDeadline = daysUntilOrderDeadline;
            dto.RecommendedOrderDate = today.AddDays(daysUntilOrderDeadline);
            dto.SafetyStockReachDate = today;
            dto.EstimatedStockoutDate = null;
            dto.AlertStatus = ProcurementAlertStatuses.Classify(daysUntilOrderDeadline);
            dto.AlertMessage =
                $"Không phát sinh tiêu hao {rollType} trong kỳ nhưng tồn kho ({FormatMm(totalRemainingMm)} mm ≈ {dto.EquivalentRollsRemaining:0.##} cây) " +
                $"đã ở mức hoặc dưới mức an toàn ({config.SafetyStockRolls:0.##} cây). Cần đặt hàng ngay.";
        }
        else
        {
            dto.RunwayDays = null;
            dto.DaysUntilOrderDeadline = null;
            dto.RecommendedOrderDate = null;
            dto.SafetyStockReachDate = null;
            dto.EstimatedStockoutDate = null;
            dto.AlertStatus = ProcurementAlertStatuses.Safe;
            dto.AlertMessage =
                $"Không phát sinh tiêu hao {rollType} trong kỳ - chưa đủ dữ liệu để dự báo ngày đặt hàng. " +
                $"Tồn kho hiện tại {FormatMm(totalRemainingMm)} mm ≈ {dto.EquivalentRollsRemaining:0.##} cây, trên mức an toàn.";
        }

        return dto;
    }

    private static string BuildAlertMessage(TypeConsumptionAndForecastDto dto)
    {
        string burnText = $"Hao ~{dto.MonthlyBurnRateRolls:0.00} cây/tháng";
        string runwayText = $"còn chạy được ~{dto.RunwayDays} ngày trước khi chạm tồn kho an toàn ({dto.SafetyStockRolls:0.##} cây)";
        string leadText = $"lead time {dto.LeadTimeDays} ngày";

        return dto.AlertStatus switch
        {
            ProcurementAlertStatuses.Critical =>
                $"{burnText}, {runwayText}, {leadText}. Đã quá hạn đặt hàng {Math.Abs(dto.DaysUntilOrderDeadline ?? 0)} ngày - ĐẶT HÀNG {dto.RollType} NGAY.",
            ProcurementAlertStatuses.Warning =>
                $"{burnText}, {runwayText}, {leadText}. Còn {dto.DaysUntilOrderDeadline} ngày đến hạn đặt hàng (trước {dto.RecommendedOrderDate:dd/MM/yyyy}).",
            _ =>
                $"{burnText}, {runwayText}, {leadText}. Hạn đặt hàng dự kiến: {dto.RecommendedOrderDate:dd/MM/yyyy}."
        };
    }

    private static int ToCappedDays(decimal days)
    {
        if (days <= 0m)
        {
            return 0;
        }

        return days >= MaxForecastDays ? MaxForecastDays : (int)days;
    }

    /// <summary>Chuẩn hóa kỳ: [FromDate.Date, ToDate.Date.AddDays(1).AddTicks(-1)].</summary>
    private static (DateTime Start, DateTime End) NormalizePeriod(DateTime fromDate, DateTime toDate)
    {
        DateTime start = fromDate.Date;
        if (toDate.Date < start)
        {
            throw new ArgumentException(
                $"Ngày bắt đầu ({fromDate:dd/MM/yyyy}) phải nhỏ hơn hoặc bằng ngày kết thúc ({toDate:dd/MM/yyyy}).",
                nameof(fromDate));
        }

        if (toDate.Date >= DateTime.MaxValue.Date)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "Ngày kết thúc không hợp lệ.");
        }

        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }

    private static string? ValidateGrindInput(DateTime grindDate, decimal postGrindDiameter, bool isAbnormal, string? remarks)
    {
        if (grindDate == default)
        {
            throw new ArgumentException("Ngày mài không hợp lệ.", nameof(grindDate));
        }

        if (grindDate.Date > DateTime.Today)
        {
            throw new ArgumentException($"Ngày mài ({grindDate:dd/MM/yyyy}) không được ở tương lai.", nameof(grindDate));
        }

        if (postGrindDiameter <= 0m)
        {
            throw new ArgumentException("ĐK sau mài phải lớn hơn 0.", nameof(postGrindDiameter));
        }

        string? normalizedRemarks = NormalizeText(remarks);
        if (isAbnormal && normalizedRemarks is null)
        {
            throw new ArgumentException("Ca mài bất thường bắt buộc phải ghi chú nguyên nhân / lỗi.", nameof(remarks));
        }

        if (normalizedRemarks is not null && normalizedRemarks.Length > MaxRemarksLength)
        {
            throw new ArgumentException($"Ghi chú không được vượt quá {MaxRemarksLength} ký tự.", nameof(remarks));
        }

        return normalizedRemarks;
    }

    private static RollEntity NormalizeRoll(RollEntity roll)
    {
        RollEntity normalized = roll.Clone();
        normalized.RollCode = NormalizeRollCode(normalized.RollCode);
        normalized.RollType = ValidateRollType(normalized.RollType);
        normalized.CommissionDate = normalized.CommissionDate.Date;
        normalized.Notes = NormalizeText(normalized.Notes);

        if (normalized.Notes is not null && normalized.Notes.Length > MaxNotesLength)
        {
            throw new ArgumentException($"Ghi chú trục không được vượt quá {MaxNotesLength} ký tự.", nameof(roll));
        }

        if (normalized.CommissionDate == default)
        {
            throw new ArgumentException("Ngày đưa vào sử dụng không hợp lệ.", nameof(roll));
        }

        return normalized;
    }

    private static void ValidateRollDimensions(RollEntity roll)
    {
        if (roll.MaxDiameter <= 0m)
        {
            throw new ArgumentException("ĐK nguyên thủy (MaxDiameter) phải lớn hơn 0.", nameof(roll));
        }

        if (roll.MinDiameter <= 0m)
        {
            throw new ArgumentException("ĐK phế phẩm (MinDiameter) phải lớn hơn 0.", nameof(roll));
        }

        if (roll.MinDiameter >= roll.MaxDiameter)
        {
            throw new ArgumentException(
                $"ĐK phế phẩm ({FormatMm(roll.MinDiameter)}) phải nhỏ hơn ĐK nguyên thủy ({FormatMm(roll.MaxDiameter)}).",
                nameof(roll));
        }

        if (roll.InitialDiameter > roll.MaxDiameter)
        {
            throw new ArgumentException(
                $"ĐK khởi tạo ({FormatMm(roll.InitialDiameter)}) không được lớn hơn ĐK nguyên thủy ({FormatMm(roll.MaxDiameter)}).",
                nameof(roll));
        }

        if (roll.InitialDiameter <= roll.MinDiameter)
        {
            throw new ArgumentException(
                $"ĐK khởi tạo ({FormatMm(roll.InitialDiameter)}) phải lớn hơn ĐK phế phẩm ({FormatMm(roll.MinDiameter)}).",
                nameof(roll));
        }

        if (roll.InitialGrindCount < 0)
        {
            throw new ArgumentException("Số lần đã mài trước khi đưa vào phần mềm không được âm.", nameof(roll));
        }
    }

    private static string NormalizeRollCode(string? rollCode)
    {
        string code = (rollCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            throw new ArgumentException("Mã trục không được để trống.", nameof(rollCode));
        }

        if (code.Length > MaxRollCodeLength)
        {
            throw new ArgumentException($"Mã trục không được vượt quá {MaxRollCodeLength} ký tự.", nameof(rollCode));
        }

        return code;
    }

    private static string ValidateRollType(string? rollType)
    {
        string type = RollTypes.Normalize(rollType);
        if (!RollTypes.IsValid(type))
        {
            throw new ArgumentException($"Loại trục '{rollType}' không hợp lệ. Chỉ chấp nhận WR, BUR hoặc IMR.", nameof(rollType));
        }

        return type;
    }

    private static string? NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }

    private void WriteAudit(SqliteConnection connection, SqliteTransaction transaction, string actionType, string targetEntity, string details)
    {
        _auditLogRepository.Insert(new AuditLogEntity
        {
            ActionTime = DateTime.Now,
            ActionType = actionType,
            TargetEntity = targetEntity,
            Details = $"[{Environment.MachineName}] [{AppSession.CurrentRole}] {details}"
        }, connection, transaction);
    }

    private static string FormatMm(decimal value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string DescribeRoll(RollEntity roll)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"RollCode={roll.RollCode}; RollType={roll.RollType}; CommissionDate={roll.CommissionDate:yyyy-MM-dd}; " +
            $"MaxDia={FormatMm(roll.MaxDiameter)}; MinDia={FormatMm(roll.MinDiameter)}; InitialDia={FormatMm(roll.InitialDiameter)}; " +
            $"InitialGrindCount={roll.InitialGrindCount}; Notes={roll.Notes ?? "(trống)"}");
    }

    private static string DescribeLog(GrindingLogEntity log)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"Id={log.Id}; RollCode={log.RollCode}; GrindDate={log.GrindDate:yyyy-MM-dd HH:mm}; " +
            $"Pre={FormatMm(log.PreGrindDiameter)}; Post={FormatMm(log.PostGrindDiameter)}; " +
            $"Consumption={FormatMm(log.PreGrindDiameter - log.PostGrindDiameter)}; " +
            $"Condition={GrindConditionTexts.From(log.IsAbnormal)}; Remarks={log.Remarks ?? "(trống)"}");
    }

    private static string DescribePeriod(ProductionPeriodEntity period)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"Id={period.Id}; PeriodName={period.PeriodName}; From={period.FromDate:yyyy-MM-dd}; To={period.ToDate:yyyy-MM-dd}; " +
            $"ProductionTons={period.ProductionTons:0.###}");
    }

    private static string DescribeConfig(ProcurementConfigEntity config)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"RollType={config.RollType}; StandardUsableMmPerRoll={config.StandardUsableMmPerRoll:0.###}; " +
            $"LeadTimeDays={config.LeadTimeDays}; SafetyStockRolls={config.SafetyStockRolls:0.###}");
    }
}
