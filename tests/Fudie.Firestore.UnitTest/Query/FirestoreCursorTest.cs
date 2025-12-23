namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreCursorTest
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDocumentIdOnly_SetsDocumentId()
    {
        var cursor = new FirestoreCursor("doc-123");

        cursor.DocumentId.Should().Be("doc-123");
    }

    [Fact]
    public void Constructor_WithDocumentIdOnly_SetsEmptyOrderByValues()
    {
        var cursor = new FirestoreCursor("doc-123");

        cursor.OrderByValues.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDocumentIdOnly_ThrowsOnNullDocumentId()
    {
        var action = () => new FirestoreCursor(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("documentId");
    }

    [Fact]
    public void Constructor_WithOrderByValues_SetsDocumentId()
    {
        var cursor = new FirestoreCursor("doc-456", 99.99, "ProductA");

        cursor.DocumentId.Should().Be("doc-456");
    }

    [Fact]
    public void Constructor_WithOrderByValues_SetsOrderByValues()
    {
        var cursor = new FirestoreCursor("doc-456", 99.99, "ProductA");

        cursor.OrderByValues.Should().HaveCount(2);
        cursor.OrderByValues[0].Should().Be(99.99);
        cursor.OrderByValues[1].Should().Be("ProductA");
    }

    [Fact]
    public void Constructor_WithOrderByValues_ThrowsOnNullDocumentId()
    {
        var action = () => new FirestoreCursor(null!, 99.99, "ProductA");

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("documentId");
    }

    [Fact]
    public void Constructor_WithNullOrderByValues_SetsEmptyArray()
    {
        var cursor = new FirestoreCursor("doc-789", null!);

        cursor.OrderByValues.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMixedTypeOrderByValues_PreservesTypes()
    {
        var date = new DateTime(2024, 1, 15);
        var cursor = new FirestoreCursor("doc-123", 100, "Name", date, 3.14);

        cursor.OrderByValues.Should().HaveCount(4);
        cursor.OrderByValues[0].Should().Be(100);
        cursor.OrderByValues[1].Should().Be("Name");
        cursor.OrderByValues[2].Should().Be(date);
        cursor.OrderByValues[3].Should().Be(3.14);
    }

    [Fact]
    public void Constructor_WithNullValueInOrderByValues_PreservesNull()
    {
        var cursor = new FirestoreCursor("doc-123", "Value1", null, "Value3");

        cursor.OrderByValues.Should().HaveCount(3);
        cursor.OrderByValues[0].Should().Be("Value1");
        cursor.OrderByValues[1].Should().BeNull();
        cursor.OrderByValues[2].Should().Be("Value3");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithDocumentIdOnly_ReturnsSimpleFormat()
    {
        var cursor = new FirestoreCursor("doc-123");

        cursor.ToString().Should().Be("Cursor(doc-123)");
    }

    [Fact]
    public void ToString_WithOrderByValues_ReturnsFullFormat()
    {
        var cursor = new FirestoreCursor("doc-456", 100, "ProductA");

        cursor.ToString().Should().Be("Cursor(doc-456, [100, ProductA])");
    }

    [Fact]
    public void ToString_WithSingleOrderByValue_ReturnsFullFormat()
    {
        var cursor = new FirestoreCursor("doc-789", "OnlyValue");

        cursor.ToString().Should().Be("Cursor(doc-789, [OnlyValue])");
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void OrderByValues_IsReadOnly()
    {
        var cursor = new FirestoreCursor("doc-123", "Value1", "Value2");

        cursor.OrderByValues.Should().BeAssignableTo<IReadOnlyList<object?>>();
    }

    #endregion
}
