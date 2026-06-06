using MetaForge.Application.DTOs;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.UnitTests;

public class LookupFieldResolverTests
{
    [Fact]
    public void InferTextField_UsesVehicleNumberWhenNameMissing()
    {
        var metadata = new EntityMetadataDto
        {
            EntityName = "Vehicle",
            Properties =
            [
                new EntityPropertyMetadataDto { Name = "Id", ClrType = "System.Int32", IsKey = true },
                new EntityPropertyMetadataDto { Name = "VehicleNumber", ClrType = "System.String" }
            ]
        };

        Assert.Equal("VehicleNumber", LookupFieldResolver.InferTextField(metadata));
    }

    [Fact]
    public void ResolveTextField_FallsBackWhenNameIsNullable()
    {
        var entityType = typeof(Domain.Features.Vehicle);

        Assert.Equal("VehicleNumber", LookupFieldResolver.ResolveTextField(entityType, null));
    }
}
