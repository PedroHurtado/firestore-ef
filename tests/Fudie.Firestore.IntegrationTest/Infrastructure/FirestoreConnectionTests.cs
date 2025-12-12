using Google.Api.Gax;

namespace Fudie.Firestore.IntegrationTest.Infrastructure;

/// <summary>
/// Tests de conexión al emulador de Firestore.
/// Requiere que el emulador esté corriendo: docker-compose up
/// </summary>
public class FirestoreConnectionTests : IAsyncLifetime
{
    private const string ProjectId = "demo-project";
    private const string EmulatorHost = "localhost:8080";

    private FirestoreDb _db = null!;

    public async Task InitializeAsync()
    {
        // Configurar el emulador de Firestore
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);

        // Crear conexión al emulador usando FirestoreDbBuilder
        _db = await new FirestoreDbBuilder
        {
            ProjectId = ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void Connection_ToEmulator_ShouldCreateFirestoreDbInstance()
    {
        // Assert
        _db.Should().NotBeNull();
        _db.ProjectId.Should().Be(ProjectId);
    }

    [Fact]
    public async Task Connection_ToEmulator_ShouldBeAbleToWriteDocument()
    {
        // Arrange
        var collectionRef = _db.Collection("integration-test");
        var documentData = new Dictionary<string, object>
        {
            { "testField", "testValue" },
            { "timestamp", Timestamp.GetCurrentTimestamp() }
        };

        // Act
        var documentRef = await collectionRef.AddAsync(documentData);

        // Assert
        documentRef.Should().NotBeNull();
        documentRef.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Connection_ToEmulator_ShouldBeAbleToReadDocument()
    {
        // Arrange
        var collectionRef = _db.Collection("integration-test-read");
        var testId = Guid.NewGuid().ToString();
        var documentData = new Dictionary<string, object>
        {
            { "id", testId },
            { "name", "Test Document" }
        };

        var docRef = await collectionRef.AddAsync(documentData);

        // Act
        var snapshot = await docRef.GetSnapshotAsync();

        // Assert
        snapshot.Exists.Should().BeTrue();
        snapshot.GetValue<string>("id").Should().Be(testId);
        snapshot.GetValue<string>("name").Should().Be("Test Document");
    }

    [Fact]
    public async Task Connection_ToEmulator_ShouldBeAbleToDeleteDocument()
    {
        // Arrange
        var collectionRef = _db.Collection("integration-test-delete");
        var documentData = new Dictionary<string, object>
        {
            { "toDelete", true }
        };

        var docRef = await collectionRef.AddAsync(documentData);

        // Verificar que existe
        var beforeDelete = await docRef.GetSnapshotAsync();
        beforeDelete.Exists.Should().BeTrue();

        // Act
        await docRef.DeleteAsync();

        // Assert
        var afterDelete = await docRef.GetSnapshotAsync();
        afterDelete.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Connection_ToEmulator_ShouldBeAbleToQueryDocuments()
    {
        // Arrange
        var collectionRef = _db.Collection("integration-test-query");
        var uniqueCategory = Guid.NewGuid().ToString();

        // Insertar documentos de prueba
        await collectionRef.AddAsync(new Dictionary<string, object>
        {
            { "category", uniqueCategory },
            { "value", 10 }
        });
        await collectionRef.AddAsync(new Dictionary<string, object>
        {
            { "category", uniqueCategory },
            { "value", 20 }
        });
        await collectionRef.AddAsync(new Dictionary<string, object>
        {
            { "category", "other" },
            { "value", 30 }
        });

        // Act
        var query = collectionRef.WhereEqualTo("category", uniqueCategory);
        var querySnapshot = await query.GetSnapshotAsync();

        // Assert
        querySnapshot.Documents.Should().HaveCount(2);
        querySnapshot.Documents.Should().AllSatisfy(doc =>
            doc.GetValue<string>("category").Should().Be(uniqueCategory));
    }
}
