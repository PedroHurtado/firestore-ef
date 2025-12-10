using System;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    public interface IFirestoreCollectionManager
    {
        string GetCollectionName(Type entityType);
    }
}
