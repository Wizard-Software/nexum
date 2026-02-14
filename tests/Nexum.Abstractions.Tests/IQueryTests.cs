namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class IQueryTests
{
    private record GetOrderQuery(Guid Id) : IQuery<string>;

    private class Animal;
    private class Dog : Animal;
    private record GetAnimalQuery : IQuery<Dog>;

    [Fact]
    public void Query_ImplementingIQueryOfT_IsIQuery()
    {
        var query = new GetOrderQuery(Guid.NewGuid());

        query.Should().BeAssignableTo<IQuery>();
        query.Should().BeAssignableTo<IQuery<string>>();
    }

    [Fact]
    public void IQueryOfT_IsCovariant()
    {
        IQuery<Dog> dogQuery = new GetAnimalQuery();
        IQuery<Animal> animalQuery = dogQuery;

        animalQuery.Should().NotBeNull();
        animalQuery.Should().BeAssignableTo<IQuery<Animal>>();
        animalQuery.Should().BeAssignableTo<IQuery>();
    }
}
