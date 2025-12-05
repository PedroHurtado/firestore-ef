namespace Fudie.Firestore.UnitTest.Conventions;

public class PrimaryKeyConventionTest
{
    private class EntityWithId
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private class Customer
    {
        public string CustomerId { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private class EntityWithBothIdTypes
    {
        public string Id { get; set; } = default!;
        public string EntityWithBothIdTypesId { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private class EntityWithoutAnyId
    {
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
    }

    [Fact]
    public void ProcessEntityTypeAdded_Sets_Id_As_PrimaryKey()
    {
        var convention = new PrimaryKeyConvention();
        string? capturedPropertyName = null;
        var (entityTypeBuilder, context) = CreateEntityTypeBuilderMock<EntityWithId>(
            onPrimaryKey: props => capturedPropertyName = props?.FirstOrDefault()?.Name);

        var entityType = entityTypeBuilder.Object.Metadata;
        SetupPropertyLookup(entityType, "Id", typeof(string));

        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        capturedPropertyName.Should().Be("Id");
    }

    [Fact]
    public void ProcessEntityTypeAdded_Sets_EntityNameId_As_PrimaryKey_When_No_Id()
    {
        var convention = new PrimaryKeyConvention();
        string? capturedPropertyName = null;
        var (entityTypeBuilder, context) = CreateEntityTypeBuilderMock<Customer>(
            onPrimaryKey: props => capturedPropertyName = props?.FirstOrDefault()?.Name);

        var entityType = entityTypeBuilder.Object.Metadata;
        SetupPropertyLookup(entityType, null, null, "CustomerId", typeof(string));

        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        capturedPropertyName.Should().Be("CustomerId");
    }

    [Fact]
    public void ProcessEntityTypeAdded_Prefers_Id_Over_EntityNameId()
    {
        var convention = new PrimaryKeyConvention();
        string? capturedPropertyName = null;
        var (entityTypeBuilder, context) = CreateEntityTypeBuilderMock<EntityWithBothIdTypes>(
            onPrimaryKey: props => capturedPropertyName = props?.FirstOrDefault()?.Name);

        var entityType = entityTypeBuilder.Object.Metadata;
        SetupPropertyLookup(entityType, "Id", typeof(string));

        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        capturedPropertyName.Should().Be("Id");
    }

    [Fact]
    public void ProcessEntityTypeAdded_Does_Nothing_When_No_Id_Property_Found()
    {
        var convention = new PrimaryKeyConvention();
        bool primaryKeyCalled = false;
        var (entityTypeBuilder, context) = CreateEntityTypeBuilderMock<EntityWithoutAnyId>(
            onPrimaryKey: _ => primaryKeyCalled = true);

        var entityType = entityTypeBuilder.Object.Metadata;
        SetupPropertyLookup(entityType, null, null);

        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        primaryKeyCalled.Should().BeFalse();
    }

    [Fact]
    public void ProcessEntityTypeAdded_Does_Nothing_When_PrimaryKey_Already_Configured()
    {
        var convention = new PrimaryKeyConvention();
        bool primaryKeyCalled = false;
        var (entityTypeBuilder, context) = CreateEntityTypeBuilderMock<EntityWithId>(
            onPrimaryKey: _ => primaryKeyCalled = true,
            hasExistingPrimaryKey: true);

        convention.ProcessEntityTypeAdded(entityTypeBuilder.Object, context.Object);

        primaryKeyCalled.Should().BeFalse();
    }

    private static (Mock<IConventionEntityTypeBuilder>, Mock<IConventionContext<IConventionEntityTypeBuilder>>)
        CreateEntityTypeBuilderMock<T>(
            Action<IReadOnlyList<IConventionProperty>?>? onPrimaryKey = null,
            bool hasExistingPrimaryKey = false)
    {
        var entityTypeMock = new Mock<IConventionEntityType>();
        entityTypeMock.Setup(e => e.ClrType).Returns(typeof(T));

        if (hasExistingPrimaryKey)
        {
            var keyMock = new Mock<IConventionKey>();
            entityTypeMock.Setup(e => e.FindPrimaryKey()).Returns(keyMock.Object);
        }
        else
        {
            entityTypeMock.Setup(e => e.FindPrimaryKey()).Returns((IConventionKey?)null);
        }

        var builderMock = new Mock<IConventionEntityTypeBuilder>();
        builderMock.Setup(b => b.Metadata).Returns(entityTypeMock.Object);

        // Setup PrimaryKey to capture the properties
        builderMock
            .Setup(b => b.PrimaryKey(It.IsAny<IReadOnlyList<IConventionProperty>>(), It.IsAny<bool>()))
            .Callback<IReadOnlyList<IConventionProperty>, bool>((props, _) => onPrimaryKey?.Invoke(props))
            .Returns((IConventionKeyBuilder?)null);

        var contextMock = new Mock<IConventionContext<IConventionEntityTypeBuilder>>();

        return (builderMock, contextMock);
    }

    private static void SetupPropertyLookup(
        IConventionEntityType entityType,
        string? idPropertyName,
        Type? idPropertyType,
        string? altIdPropertyName = null,
        Type? altIdPropertyType = null)
    {
        var entityTypeMock = Mock.Get(entityType);

        if (idPropertyName != null && idPropertyType != null)
        {
            var propertyMock = new Mock<IConventionProperty>();
            propertyMock.Setup(p => p.Name).Returns(idPropertyName);
            propertyMock.Setup(p => p.ClrType).Returns(idPropertyType);
            entityTypeMock.Setup(e => e.FindProperty("Id")).Returns(propertyMock.Object);
        }
        else
        {
            entityTypeMock.Setup(e => e.FindProperty("Id")).Returns((IConventionProperty?)null);
        }

        if (altIdPropertyName != null && altIdPropertyType != null)
        {
            var altPropertyMock = new Mock<IConventionProperty>();
            altPropertyMock.Setup(p => p.Name).Returns(altIdPropertyName);
            altPropertyMock.Setup(p => p.ClrType).Returns(altIdPropertyType);
            entityTypeMock.Setup(e => e.FindProperty(altIdPropertyName)).Returns(altPropertyMock.Object);
        }
        else
        {
            var entityName = entityType.ClrType.Name;
            entityTypeMock.Setup(e => e.FindProperty($"{entityName}Id")).Returns((IConventionProperty?)null);
        }
    }
}
