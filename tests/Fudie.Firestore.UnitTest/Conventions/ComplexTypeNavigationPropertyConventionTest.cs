namespace Fudie.Firestore.UnitTest.Conventions;

public class ComplexTypeNavigationPropertyConventionTest
{
    private class Address
    {
        public string Street { get; set; } = default!;
        public string City { get; set; } = default!;
    }

    private class Customer
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    private class AddressWithNavigation
    {
        public string Street { get; set; } = default!;
        public Customer Customer { get; set; } = default!;
    }

    private class AddressWithCollection
    {
        public string Street { get; set; } = default!;
        public List<Customer> Customers { get; set; } = default!;
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Does_Not_Ignore_Simple_Properties()
    {
        // Arrange
        var convention = new ComplexTypeNavigationPropertyConvention();
        var (propertyBuilder, context, ignoredProperties) = CreateComplexPropertyBuilderMock(
            typeof(Address),
            entityTypes: Array.Empty<Type>());

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        ignoredProperties().Should().BeEmpty();
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Ignores_Navigation_Property()
    {
        // Arrange
        var convention = new ComplexTypeNavigationPropertyConvention();
        var (propertyBuilder, context, ignoredProperties) = CreateComplexPropertyBuilderMock(
            typeof(AddressWithNavigation),
            entityTypes: new[] { typeof(Customer) });

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        ignoredProperties().Should().Contain("Customer");
    }

    [Fact]
    public void ProcessComplexPropertyAdded_Ignores_Collection_Navigation_Property()
    {
        // Arrange
        var convention = new ComplexTypeNavigationPropertyConvention();
        var (propertyBuilder, context, ignoredProperties) = CreateComplexPropertyBuilderMock(
            typeof(AddressWithCollection),
            entityTypes: new[] { typeof(Customer) });

        // Act
        convention.ProcessComplexPropertyAdded(propertyBuilder.Object, context.Object);

        // Assert
        ignoredProperties().Should().Contain("Customers");
    }

    private static (Mock<IConventionComplexPropertyBuilder>, Mock<IConventionContext<IConventionComplexPropertyBuilder>>, Func<List<string>>)
        CreateComplexPropertyBuilderMock(Type complexClrType, Type[] entityTypes)
    {
        var ignoredProps = new List<string>();

        var complexTypeMock = new Mock<IConventionComplexType>();
        var complexTypeBuilderMock = new Mock<IConventionComplexTypeBuilder>();
        var complexPropertyMock = new Mock<IConventionComplexProperty>();
        var declaringTypeMock = new Mock<IConventionTypeBase>();
        var modelMock = new Mock<IConventionModel>();
        var propertyBuilderMock = new Mock<IConventionComplexPropertyBuilder>();
        var contextMock = new Mock<IConventionContext<IConventionComplexPropertyBuilder>>();

        complexTypeMock.Setup(t => t.ClrType).Returns(complexClrType);
        complexTypeMock.Setup(t => t.Builder).Returns(complexTypeBuilderMock.Object);
        complexTypeMock.Setup(t => t.GetComplexProperties()).Returns(Array.Empty<IConventionComplexProperty>());

        complexTypeBuilderMock
            .Setup(b => b.Ignore(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((name, _) => ignoredProps.Add(name))
            .Returns(complexTypeBuilderMock.Object);

        foreach (var entityType in entityTypes)
        {
            var entityTypeMock = new Mock<IConventionEntityType>();
            modelMock.Setup(m => m.FindEntityType(entityType)).Returns(entityTypeMock.Object);
        }

        declaringTypeMock.Setup(t => t.Model).Returns(modelMock.Object);
        complexPropertyMock.Setup(p => p.ComplexType).Returns(complexTypeMock.Object);
        complexPropertyMock.Setup(p => p.DeclaringType).Returns(declaringTypeMock.Object);
        propertyBuilderMock.Setup(b => b.Metadata).Returns(complexPropertyMock.Object);

        return (propertyBuilderMock, contextMock, () => ignoredProps);
    }
}
