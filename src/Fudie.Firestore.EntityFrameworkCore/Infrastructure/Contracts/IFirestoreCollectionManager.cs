using System;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
{
    public interface IFirestoreCollectionManager
    {
        string GetCollectionName(Type entityType);
    }
}
