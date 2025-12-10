namespace Firestore.EntityFrameworkCore.Infrastructure
{
    public interface IFirestoreIdGenerator
    {
        string GenerateId();
    }
}
