namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class IStreamQueryTests
{
    private class Animal;
    private class Dog : Animal;
    private record StreamAnimalsQuery : IStreamQuery<Dog>;

    [Fact]
    public void StreamQuery_ImplementingIStreamQueryOfT_IsNotIQuery()
    {
        var query = new StreamAnimalsQuery();

        query.Should().BeAssignableTo<IStreamQuery<Dog>>();
        query.Should().NotBeAssignableTo<IQuery>();
        query.Should().NotBeAssignableTo<IQuery<Dog>>();
    }

    [Fact]
    public void IStreamQueryOfT_IsCovariant()
    {
        IStreamQuery<Dog> dogQuery = new StreamAnimalsQuery();
        IStreamQuery<Animal> animalQuery = dogQuery;

        animalQuery.Should().NotBeNull();
        animalQuery.Should().BeAssignableTo<IStreamQuery<Animal>>();
    }
}
