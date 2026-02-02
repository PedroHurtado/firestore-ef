using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fudie.Firestore.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Interceptor that synchronizes ArrayOf shadow properties and fixes SubCollection states before SaveChanges.
/// This ensures that:
/// 1. Changes to ArrayOf properties are detected and the entity is marked as Modified
/// 2. SubCollection entities removed from parent collections are correctly marked as Deleted
/// </summary>
public class ArrayOfSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            ArrayOfChangeTracker.SyncArrayOfChanges(eventData.Context);
            SubCollectionChangeTracker.FixSubCollectionDeleteState(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            ArrayOfChangeTracker.SyncArrayOfChanges(eventData.Context);
            SubCollectionChangeTracker.FixSubCollectionDeleteState(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
