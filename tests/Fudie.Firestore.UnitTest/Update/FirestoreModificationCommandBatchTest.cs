using Fudie.Firestore.EntityFrameworkCore.Update;

namespace Fudie.Firestore.UnitTest.Update;

public class FirestoreModificationCommandBatchFactoryTest
{
    #region Interface Implementation Tests

    [Fact]
    public void FirestoreModificationCommandBatchFactory_Implements_IModificationCommandBatchFactory()
    {
        typeof(FirestoreModificationCommandBatchFactory)
            .Should().Implement<IModificationCommandBatchFactory>();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_Has_Dependencies_Parameter()
    {
        var constructors = typeof(FirestoreModificationCommandBatchFactory).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(ModificationCommandBatchFactoryDependencies));
    }

    [Fact]
    public void Constructor_Parameter_Is_Named_Dependencies()
    {
        var constructor = typeof(FirestoreModificationCommandBatchFactory).GetConstructors()[0];

        constructor.GetParameters()[0].Name.Should().Be("dependencies");
    }

    #endregion

    #region Create Method Tests

    [Fact]
    public void Create_Method_Exists()
    {
        var method = typeof(FirestoreModificationCommandBatchFactory).GetMethod("Create");

        method.Should().NotBeNull();
    }

    [Fact]
    public void Create_Method_Returns_ModificationCommandBatch()
    {
        var method = typeof(FirestoreModificationCommandBatchFactory).GetMethod("Create");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ModificationCommandBatch));
    }

    [Fact]
    public void Create_Method_Has_No_Parameters()
    {
        var method = typeof(FirestoreModificationCommandBatchFactory).GetMethod("Create");

        method.Should().NotBeNull();
        method!.GetParameters().Should().BeEmpty();
    }

    #endregion
}

public class FirestoreModificationCommandBatchTest
{
    #region Inheritance Tests

    [Fact]
    public void FirestoreModificationCommandBatch_Inherits_From_SingularModificationCommandBatch()
    {
        typeof(FirestoreModificationCommandBatch)
            .Should().BeDerivedFrom<SingularModificationCommandBatch>();
    }

    [Fact]
    public void FirestoreModificationCommandBatch_Inherits_From_ModificationCommandBatch()
    {
        typeof(FirestoreModificationCommandBatch)
            .Should().BeDerivedFrom<ModificationCommandBatch>();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_Has_Dependencies_Parameter()
    {
        var constructors = typeof(FirestoreModificationCommandBatch).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(ModificationCommandBatchFactoryDependencies));
    }

    [Fact]
    public void Constructor_Parameter_Is_Named_Dependencies()
    {
        var constructor = typeof(FirestoreModificationCommandBatch).GetConstructors()[0];

        constructor.GetParameters()[0].Name.Should().Be("dependencies");
    }

    #endregion

    #region Type Structure Tests

    [Fact]
    public void FirestoreModificationCommandBatch_Is_Public_Class()
    {
        typeof(FirestoreModificationCommandBatch).IsPublic.Should().BeTrue();
        typeof(FirestoreModificationCommandBatch).IsClass.Should().BeTrue();
    }

    [Fact]
    public void FirestoreModificationCommandBatchFactory_Is_Public_Class()
    {
        typeof(FirestoreModificationCommandBatchFactory).IsPublic.Should().BeTrue();
        typeof(FirestoreModificationCommandBatchFactory).IsClass.Should().BeTrue();
    }

    #endregion

    #region Namespace Tests

    [Fact]
    public void Classes_Are_In_Correct_Namespace()
    {
        typeof(FirestoreModificationCommandBatch).Namespace
            .Should().Be("Fudie.Firestore.EntityFrameworkCore.Update");

        typeof(FirestoreModificationCommandBatchFactory).Namespace
            .Should().Be("Fudie.Firestore.EntityFrameworkCore.Update");
    }

    #endregion
}
