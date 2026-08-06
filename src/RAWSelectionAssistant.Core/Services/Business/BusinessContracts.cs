using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Business;

public interface IBookingPeopleRepository
{
    Task<IReadOnlyList<BookingContact>> ListContactsAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingStaffMember>> ListStaffAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task ReplaceAsync(Guid bookingId, IReadOnlyList<BookingContact> contacts, IReadOnlyList<BookingStaffMember> staff, CancellationToken cancellationToken = default);
}

public interface IBookingPeopleService
{
    Task<IReadOnlyList<BookingContact>> ListContactsAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingStaffMember>> ListStaffAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid bookingId, IReadOnlyList<BookingContact> contacts, IReadOnlyList<BookingStaffMember> staff, CancellationToken cancellationToken = default);
}

public interface IFinanceRepository
{
    Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(bool includeDisabled = false, CancellationToken cancellationToken = default);
    Task<FinanceTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTransaction>> QueryAsync(FinanceQuery query, CancellationToken cancellationToken = default);
}

public interface IFinanceService
{
    Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(bool includeDisabled = false, CancellationToken cancellationToken = default);
    Task<FinanceTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinanceTransaction> SaveAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, bool userConfirmed, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTransaction>> QueryAsync(FinanceQuery query, CancellationToken cancellationToken = default);
    Task<FinanceSummary> SummarizeAsync(FinanceQuery query, CancellationToken cancellationToken = default);
    Task ExportCsvAsync(string outputPath, FinanceQuery query, CancellationToken cancellationToken = default);
}
