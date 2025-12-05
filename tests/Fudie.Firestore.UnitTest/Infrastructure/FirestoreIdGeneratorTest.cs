using Firestore.EntityFrameworkCore.Infrastructure.Internal;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreIdGeneratorTest
{
    private const string ValidCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int ExpectedLength = 20;

    [Fact]
    public void GenerateId_Returns_Id_With_Correct_Length()
    {
        var generator = new FirestoreIdGenerator();

        var id = generator.GenerateId();

        id.Should().HaveLength(ExpectedLength);
    }

    [Fact]
    public void GenerateId_Returns_Id_With_Only_Valid_Characters()
    {
        var generator = new FirestoreIdGenerator();

        var id = generator.GenerateId();

        id.Should().MatchRegex($"^[{ValidCharacters}]+$");
    }

    [Fact]
    public void GenerateId_Returns_Different_Ids_On_Multiple_Calls()
    {
        var generator = new FirestoreIdGenerator();

        var id1 = generator.GenerateId();
        var id2 = generator.GenerateId();
        var id3 = generator.GenerateId();

        id1.Should().NotBe(id2);
        id2.Should().NotBe(id3);
        id1.Should().NotBe(id3);
    }

    [Fact]
    public void GenerateId_Returns_Unique_Ids_In_Bulk()
    {
        var generator = new FirestoreIdGenerator();
        var ids = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            ids.Add(generator.GenerateId());
        }

        ids.Should().HaveCount(1000, "all generated IDs should be unique");
    }

    [Fact]
    public void GenerateId_Uses_All_Character_Types()
    {
        var generator = new FirestoreIdGenerator();
        var allIds = string.Concat(Enumerable.Range(0, 100).Select(_ => generator.GenerateId()));

        allIds.Should().ContainAny("A", "B", "C", "Z"); // uppercase
        allIds.Should().ContainAny("a", "b", "c", "z"); // lowercase
        allIds.Should().ContainAny("0", "1", "9");      // digits
    }

    [Fact]
    public void GenerateId_Never_Returns_Null_Or_Empty()
    {
        var generator = new FirestoreIdGenerator();

        for (int i = 0; i < 100; i++)
        {
            var id = generator.GenerateId();
            id.Should().NotBeNullOrEmpty();
        }
    }
}
