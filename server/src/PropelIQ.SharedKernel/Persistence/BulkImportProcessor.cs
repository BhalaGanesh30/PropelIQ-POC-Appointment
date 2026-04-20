using Microsoft.EntityFrameworkCore;
using PropelIQ.SharedKernel.Persistence.Exceptions;

namespace PropelIQ.SharedKernel.Persistence;

/// <summary>
/// Wraps an entire batch of entities in a single transaction.
/// When a referential integrity violation occurs mid-batch, the transaction
/// rolls back and an error report identifies the offending rows (edge case).
/// </summary>
public sealed class BulkImportProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly DbContext _context;

    public BulkImportProcessor(IUnitOfWork unitOfWork, DbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<BulkImportResult> ImportAsync<T>(
        IReadOnlyList<T> entities,
        CancellationToken cancellationToken = default) where T : class
    {
        var errors = new List<BulkImportError>();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            for (int i = 0; i < entities.Count; i++)
            {
                _context.Set<T>().Add(entities[i]);

                // Flush per-row to detect FK violations early and identify offending rows.
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    var translated = DatabaseErrorHandler.Translate(ex);
                    if (translated is ReferentialIntegrityException riEx)
                    {
                        errors.Add(new BulkImportError(
                            RowIndex: i,
                            EntityType: typeof(T).Name,
                            ErrorMessage: riEx.Message,
                            ConstraintName: riEx.ConstraintName));

                        // Detach failed entity to continue validation of remaining rows.
                        _context.Entry(entities[i]).State = EntityState.Detached;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (errors.Count > 0)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw new BulkImportException(
                    $"Bulk import failed: {errors.Count} row(s) violated referential integrity.",
                    errors);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return new BulkImportResult(entities.Count, 0);
        }
        catch (BulkImportException)
        {
            throw; // Already rolled back above.
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

public sealed record BulkImportResult(int TotalRows, int FailedRows);
