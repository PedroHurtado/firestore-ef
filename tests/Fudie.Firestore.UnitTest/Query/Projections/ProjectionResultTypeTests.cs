using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Projections
{
    /// <summary>
    /// Tests for ProjectionResultType enum.
    /// Verifies all projection types are defined correctly.
    /// </summary>
    public class ProjectionResultTypeTests
    {
        [Fact]
        public void ProjectionResultType_HasEntityValue()
        {
            // e => e
            var value = ProjectionResultType.Entity;
            Assert.Equal(0, (int)value);
        }

        [Fact]
        public void ProjectionResultType_HasSingleFieldValue()
        {
            // e => e.Name
            var value = ProjectionResultType.SingleField;
            Assert.Equal(1, (int)value);
        }

        [Fact]
        public void ProjectionResultType_HasAnonymousTypeValue()
        {
            // e => new { e.Id, e.Name }
            var value = ProjectionResultType.AnonymousType;
            Assert.Equal(2, (int)value);
        }

        [Fact]
        public void ProjectionResultType_HasDtoClassValue()
        {
            // e => new Dto { Id = e.Id }
            var value = ProjectionResultType.DtoClass;
            Assert.Equal(3, (int)value);
        }

        [Fact]
        public void ProjectionResultType_HasRecordValue()
        {
            // e => new Record(e.Id, e.Name)
            var value = ProjectionResultType.Record;
            Assert.Equal(4, (int)value);
        }

        [Fact]
        public void ProjectionResultType_HasFiveValues()
        {
            var values = Enum.GetValues<ProjectionResultType>();
            Assert.Equal(5, values.Length);
        }
    }
}
